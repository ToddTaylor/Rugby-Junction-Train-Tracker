# Stubbed Authentication Endpoints

These endpoints are referenced by the frontend but currently stubbed. Implement them in the backend Web project.

## POST /api/v1/auth/send-code
Request body:
```json
{ "email": "user@example.com" }
```
Behavior:
- Generate a 6 digit numeric code.
- Store hashed code with expiration (e.g. 10 minutes) keyed by email.
- Send code via email provider (SMTP or API).
- Response: `{ "success": true }` or `{ "success": false, "errors": ["message"] }`.

## POST /api/v1/auth/verify-code
Request body:
```json
{ "email": "user@example.com", "code": "123456", "remember": true }
```
Behavior:
- Validate code: correct & not expired.
- Issue auth token (JWT or opaque) valid for session length.
- If `remember` true, set expiry to 1 year; else a shorter duration.
- Response:
```json
{ "success": true, "token": "jwt-or-opaque", "expiresUtc": "2026-11-01T00:00:00.000Z" }
```
or failure:
```json
{ "success": false, "errors": ["Invalid code"] }
```

## Notes
- Token is stored in cookie (frontend sets cookie). For security, prefer httpOnly cookie set by server.
- Rate limit send-code to prevent abuse (e.g. 5 per hour per email/IP).
- Consider also implementing logout endpoint to invalidate tokens (server-side blacklisting if opaque).
- Add audit logging for code sends and verifications.
