# SMTP and Google OAuth Production Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable Gmail OTP email and Google sign-in on `https://domixi.click` using Plesk environment variables without committing secrets.

**Architecture:** Keep the existing `SmtpOptions` and conditional Google authentication code. Create an isolated Google Cloud project and Web OAuth client, create a Gmail App Password, then enter all runtime values in Plesk's `.NET Core → Environment variables` editor. The repository remains secret-free.

**Tech Stack:** ASP.NET Core .NET 10, Gmail SMTP (`smtp.gmail.com:587`), Google OAuth 2.0, Plesk Obsidian .NET Core settings.

## Global Constraints

- Use the dedicated Google Cloud project `LTWNC English`; do not modify the existing project `1234` clients.
- Use a Gmail App Password, never the normal Gmail account password.
- Do not paste any password, App Password, Client Secret, or API key into chat or Git.
- Use `https://domixi.click/signin-google` as the OAuth callback.
- Store production values only as Plesk environment variables.
- Preserve the existing `Production` environment setting and all production files/data.

---

### Task 1: Create the isolated Google Cloud project and OAuth client

**Files:**
- Modify: Google Cloud Console only; no repository file changes.

**Interfaces:**
- Produces: OAuth Web client values for `Authentication__Google__ClientId` and `Authentication__Google__ClientSecret`.

- [ ] **Step 1: Create the project**

Open `https://console.cloud.google.com/projectcreate`, set project name to `LTWNC English`, and create it. Switch the Cloud Console project selector to this new project.

- [ ] **Step 2: Configure the OAuth consent screen**

Open **APIs & Services → OAuth consent screen**. Choose **External**, set the application name to `LTWNC English`, use the signed-in Gmail address for support/developer contact, and add the `openid`, `email`, and `profile` scopes. Add the same Gmail account as a test user while the app remains in testing mode.

- [ ] **Step 3: Create the Web OAuth client**

Open **APIs & Services → Credentials → Create credentials → OAuth client ID**. Select **Web application**, name it `LTWNC English Web`, set:

```text
Authorized JavaScript origins:
https://domixi.click

Authorized redirect URIs:
https://domixi.click/signin-google
```

Create the client. Keep the Client ID and Client Secret private for Task 3; do not paste them into chat.

- [ ] **Step 4: Verify the client settings**

Reopen the new client and confirm it is a Web application and the redirect URI is exactly `/signin-google` over HTTPS. Do not change the existing `MemoDeck` or `TraiCay` clients in project `1234`.

### Task 2: Create the Gmail SMTP App Password

**Files:**
- Modify: Google Account security settings only; no repository file changes.

**Interfaces:**
- Produces: a Gmail App Password for `Smtp__Password`.

- [ ] **Step 1: Open App Passwords**

Open `https://myaccount.google.com/apppasswords` for the sender Gmail account. Two-step verification is required.

- [ ] **Step 2: Create a labeled password**

Enter `LTWNC English SMTP` as the application label and create the App Password. Copy it to a private password manager or clipboard for Task 3. Do not send it in chat.

- [ ] **Step 3: Confirm the sender values**

Use the same Gmail address for both `Smtp__UserName` and `Smtp__From`. Use `smtp.gmail.com`, port `587`, and SSL enabled.

### Task 3: Configure production variables in Plesk

**Files:**
- Modify: Plesk domain `domixi.click` `.NET Core` environment variables only.

**Interfaces:**
- Consumes: Gmail address/App Password from Task 2 and OAuth Client ID/Secret from Task 1.
- Produces: runtime configuration consumed by `Program.cs`, `SmtpOptions`, and `GoogleAuthSettings`.

- [ ] **Step 1: Open the environment editor**

In Plesk open `domixi.click → .NET Core → Environment → Edit...`. Keep the existing variable `ASPNETCORE_ENVIRONMENT=Production` unchanged.

- [ ] **Step 2: Add SMTP variables**

Add these exact name/value pairs, entering the private values directly into Plesk:

```text
Smtp__Host          = smtp.gmail.com
Smtp__Port          = 587
Smtp__UserName      = the sender Gmail address from Task 2
Smtp__Password      = the Gmail App Password from Task 2
Smtp__From          = the sender Gmail address from Task 2
Smtp__EnableSsl     = true
```

- [ ] **Step 3: Add Google variables**

Add these exact name/value pairs using the client created in Task 1:

```text
Authentication__Google__ClientId      = the Client ID created in Task 1
Authentication__Google__ClientSecret  = the Client Secret created in Task 1
```

- [ ] **Step 4: Save and restart**

Click **Save** in the Plesk environment editor. Restart the `.NET Core` application from Plesk if a restart control is available; otherwise wait for the application pool to recycle after saving.

### Task 4: Verify the live flows

**Files:**
- Test: live site only; no repository file changes.

**Interfaces:**
- Verifies: SMTP OTP delivery and Google OAuth callback.

- [ ] **Step 1: Verify configuration activation**

Open `https://domixi.click/Account/Login` and confirm the `Tiếp tục với Google` button is visible. Its visibility proves both Google configuration values were loaded.

- [ ] **Step 2: Verify Google sign-in**

Click the Google button, authorize the test account, and confirm Google returns to `https://domixi.click/signin-google` and then the user landing page.

- [ ] **Step 3: Verify SMTP OTP**

Open registration or password reset, request an OTP, and confirm the message arrives from the configured Gmail sender. Do not log the App Password or OAuth Secret.

- [ ] **Step 4: Verify the unaffected path**

Confirm normal local username/password login still works. If a provider fails, remove or replace only the corresponding Plesk variables; no database migration or code rollback is needed.
