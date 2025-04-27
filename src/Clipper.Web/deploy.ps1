# Azure App Service Deployment Script

# Ensure the web app is stopped before deployment
Write-Output "Stopping the web app..."
az webapp stop --name clipsagewebapp --resource-group ClipSageResourceGroup

# Deploy the web app
Write-Output "Deploying the web app..."
dotnet publish -c Release -o ./publish

# Ensure proper configuration for authentication
Write-Output "Configuring authentication settings..."
az webapp config appsettings set --name clipsagewebapp --resource-group ClipSageResourceGroup --settings "Authentication:Google:ClientId=your-google-client-id"
az webapp config appsettings set --name clipsagewebapp --resource-group ClipSageResourceGroup --settings "Authentication:Google:ClientSecret=your-google-client-secret"
az webapp config appsettings set --name clipsagewebapp --resource-group ClipSageResourceGroup --settings "Authentication:Microsoft:ClientId=5162d442-191b-4ed7-88c5-92e9e5a5d1ff"
az webapp config appsettings set --name clipsagewebapp --resource-group ClipSageResourceGroup --settings "Authentication:Microsoft:ClientSecret=9V48Q~dpuHHA~73jLCrkFYXP5cqP70WpHmR~jab4"

# Start the web app
Write-Output "Starting the web app..."
az webapp start --name clipsagewebapp --resource-group ClipSageResourceGroup

Write-Output "Deployment completed successfully!"
