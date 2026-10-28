# Deploying to Azure (free tier)

The app deploys as **one Azure App Service** (API + built React app) backed by **Azure SQL Database**.
GitHub Actions deploys automatically on every push to `main` once the steps below are done.

| Resource | Tier | Cost |
|---|---|---|
| App Service plan (Linux) | **F1 Free** | Free (60 CPU-minutes/day, sleeps when idle) |
| Azure SQL Database | **Free offer** (serverless, 100k vCore-seconds + 32 GB per month) | Free; auto-pauses if the monthly allowance runs out |
| OpenAI / Azure OpenAI | Optional | The app runs in mock AI mode without a key |

## 1. Create the resources

You need the [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) and `az login`.
Pick globally unique names for the web app and SQL server.

```bash
RG=servicedesk-rg
LOCATION=australiaeast
APP=servicedesk-<yourname>          # becomes https://servicedesk-<yourname>.azurewebsites.net
SQL_SERVER=servicedesk-sql-<yourname>
SQL_ADMIN=sdadmin
SQL_PASSWORD='<a strong password>'

az group create --name $RG --location $LOCATION

# App Service: free Linux plan + .NET 9 web app
az appservice plan create --resource-group $RG --name servicedesk-plan --sku F1 --is-linux
az webapp create --resource-group $RG --plan servicedesk-plan --name $APP --runtime "DOTNETCORE:9.0"
az webapp update --resource-group $RG --name $APP --https-only true

# Azure SQL: logical server + database using the free offer
az sql server create --resource-group $RG --name $SQL_SERVER --location $LOCATION \
  --admin-user $SQL_ADMIN --admin-password "$SQL_PASSWORD"
# "Allow Azure services" so App Service can reach the database
az sql server firewall-rule create --resource-group $RG --server $SQL_SERVER \
  --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
az sql db create --resource-group $RG --server $SQL_SERVER --name servicedesk \
  --edition GeneralPurpose --compute-model Serverless --family Gen5 --capacity 2 \
  --use-free-limit --free-limit-exhaustion-behavior AutoPause
```

> If your CLI version doesn't know `--use-free-limit`, create the database in the Azure Portal instead:
> *SQL databases → Create → "Apply offer"* on the free offer banner.

## 2. Configure the app (secrets live here, never in the repo)

```bash
az webapp config appsettings set --resource-group $RG --name $APP --settings \
  Database__Provider=SqlServer \
  Jwt__SigningKey="$(openssl rand -base64 48)" \
  Integrations__PublicUrl="https://$APP.azurewebsites.net" \
  AI__Provider=Mock

az webapp config connection-string set --resource-group $RG --name $APP --connection-string-type SQLAzure \
  --settings Default="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=servicedesk;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;Encrypt=True;Connection Timeout=60;"

# Health check: App Service pings /health and restarts unhealthy instances
az webapp config set --resource-group $RG --name $APP --generic-configurations '{"healthCheckPath": "/health"}'
```

Optional settings:

| Setting | Purpose |
|---|---|
| `AI__Provider` = `OpenAI` or `AzureOpenAI`, `AI__ApiKey`, `AI__ChatModel`, `AI__EmbeddingModel`, `AI__Endpoint` | Real LLM triage instead of mock mode |
| `Integrations__TeamsWebhookUrl` | Teams channel notifications (create a Teams *Workflows* webhook: "Post to a channel when a webhook request is received") |
| `Integrations__InboundEmailSecret` | Enables `POST /api/integrations/email/inbound` |

On first start the app applies the EF Core migrations and seeds demo data into the empty database.

## 3. Connect GitHub Actions

The deploy job uses a **publish profile**. New web apps have basic-auth publishing disabled, so enable it first:

```bash
az resource update --resource-group $RG --name scm --namespace Microsoft.Web \
  --resource-type basicPublishingCredentialsPolicies --parent sites/$APP --set properties.allow=true

az webapp deployment list-publishing-profiles --resource-group $RG --name $APP --xml > publish-profile.xml
```

In the GitHub repo, **Settings → Secrets and variables → Actions**:

- **Secret** `AZURE_WEBAPP_PUBLISH_PROFILE` = the contents of `publish-profile.xml` (then delete the file)
- **Variable** `AZURE_WEBAPP_NAME` = `$APP`

Push to `main` (or re-run the latest workflow). The **Deploy to Azure App Service** job publishes the API with
the React build in `wwwroot`, deploys it, and smoke-tests `/health`.

## Free-tier behaviour to expect

- **Cold starts:** F1 apps sleep after ~20 minutes idle; the first request takes 20–40 s. A paused serverless
  database adds up to a minute more. EF Core is configured with `EnableRetryOnFailure` for this.
- **Background jobs** (AI triage queue, Teams notification timer) only run while the app is awake.
  Production would use *Always On* (Basic tier) or move them to Azure Functions.
- **Demo data is shared:** anyone with the demo logins can change it. Delete and recreate the database to reset it.

## Tear down

```bash
az group delete --name $RG
```
