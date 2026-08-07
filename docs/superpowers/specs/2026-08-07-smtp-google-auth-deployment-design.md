# SMTP and Google OAuth Production Configuration

## Goal
Enable Gmail SMTP OTP delivery and Google sign-in for `https://domixi.click` without storing credentials in Git.

## Design
- Create a dedicated Google Cloud project named `LTWNC English`.
- Configure a Web OAuth client with:
  - Authorized JavaScript origin: `https://domixi.click`
  - Authorized redirect URI: `https://domixi.click/signin-google`
- Use a Gmail App Password for the existing `smtp.gmail.com:587` sender.
- Store production settings as Plesk/ASP.NET Core environment variables:
  - `Smtp__Host=smtp.gmail.com`
  - `Smtp__Port=587`
  - `Smtp__UserName`
  - `Smtp__Password`
  - `Smtp__From`
  - `Smtp__EnableSsl=true`
  - `Authentication__Google__ClientId`
  - `Authentication__Google__ClientSecret`

The application already binds these configuration sections and registers Gmail SMTP and Google authentication conditionally. No application code or repository configuration needs to contain secrets.

## Flow
1. Google Cloud project and OAuth consent/client are created.
2. Gmail App Password is created privately.
3. Values are entered only in the production Plesk environment configuration.
4. The live site is tested for OTP email and Google login.

## Verification and rollback
- Verify a registration/password-reset OTP arrives and Google login returns through `/signin-google`.
- Verify normal local login remains unchanged.
- Rollback by removing or replacing the Plesk environment variables; no database migration is required.
