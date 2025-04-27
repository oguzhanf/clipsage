# Authentication Troubleshooting Guide

This document provides guidance for troubleshooting authentication issues with the ClipSage web application.

## Common Issues and Solutions

### 1. Service Unavailable (HTTP Error 503)

**Symptoms:**
- "Service Unavailable" error page
- HTTP Error 503 message
- Error occurs after authentication redirect

**Possible Causes:**
- Azure App Service is stopped or experiencing issues
- Resource constraints (CPU/memory limits exceeded)
- Application startup errors
- Misconfigured authentication settings

**Solutions:**
1. Check Azure App Service status in Azure Portal
2. Restart the App Service
3. Check App Service logs for errors
4. Verify authentication configuration in Azure App Settings

### 2. Authentication Provider Configuration

**Symptoms:**
- Authentication fails with provider-specific errors
- Redirect loops during authentication
- "Error from external provider" messages

**Solutions:**
1. Verify client ID and secret are correctly configured in Azure App Settings
2. Check that redirect URIs are properly registered in the provider's developer console
3. Ensure the authentication provider service is operational

### 3. Redirect URI Issues

**Symptoms:**
- Error about invalid redirect URI
- Authentication starts but fails during callback

**Solutions:**
1. Register the correct redirect URI in the provider's developer console:
   - Google: `https://clipsagewebapp.azurewebsites.net/signin-google`
   - Microsoft: `https://clipsagewebapp.azurewebsites.net/signin-microsoft`
   - Facebook: `https://clipsagewebapp.azurewebsites.net/signin-facebook`

2. Ensure the callback path in the application matches the registered redirect URI

## Diagnostic Steps

1. **Check Application Logs:**
   - In Azure Portal, navigate to the App Service
   - Go to "Diagnose and solve problems"
   - Check "Application Logs" for errors

2. **Test Authentication Locally:**
   - Run the application locally with proper configuration
   - Use development authentication providers
   - Check for any errors in the console output

3. **Verify Azure Configuration:**
   - Check App Service configuration
   - Verify environment variables and app settings
   - Ensure authentication settings are properly configured

## Contact Support

If you continue to experience issues after trying these solutions, please contact support with the following information:
- Exact error message
- Steps to reproduce the issue
- Authentication provider being used
- Browser and operating system details
