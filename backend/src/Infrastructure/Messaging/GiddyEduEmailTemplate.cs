using System.Net;

namespace GiddyEdu.Infrastructure.Messaging;

public sealed record EmailContent(string HtmlBody, string TextBody);

public static class GiddyEduEmailTemplate
{
    public static EmailContent Create(string heading, string message, string? actionLabel = null, string? actionUrl = null, string? verificationCode = null, string? supportingText = null)
    {
        var safeHeading = Encode(heading);
        var safeMessage = Encode(message);
        var action = string.IsNullOrWhiteSpace(actionLabel) || string.IsNullOrWhiteSpace(actionUrl) ? string.Empty : $"""
            <tr><td style="padding:4px 48px 30px">
              <table role="presentation" cellspacing="0" cellpadding="0"><tr><td style="border-radius:12px;background:#12372a">
                <a href="{Encode(actionUrl)}" style="display:inline-block;padding:15px 24px;color:#ffffff;text-decoration:none;font-size:15px;line-height:20px;font-weight:700">{Encode(actionLabel)} &nbsp;→</a>
              </td></tr></table>
            </td></tr>
            """;
        var code = string.IsNullOrWhiteSpace(verificationCode) ? string.Empty : $"""
            <tr><td style="padding:4px 48px 30px">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border:1px solid #d9e4dc;border-radius:16px;background:#f3f7f4">
                <tr><td align="center" style="padding:18px 20px 4px;color:#557064;font-size:11px;line-height:16px;font-weight:700;letter-spacing:1.8px;text-transform:uppercase">Your verification code</td></tr>
                <tr><td align="center" style="padding:2px 20px 18px;color:#12372a;font-family:Consolas,Menlo,monospace;font-size:34px;line-height:44px;font-weight:800;letter-spacing:9px">{Encode(verificationCode)}</td></tr>
              </table>
            </td></tr>
            """;
        var supporting = string.IsNullOrWhiteSpace(supportingText) ? string.Empty : $"<tr><td style=\"padding:0 48px 34px;color:#64736b;font-size:13px;line-height:21px\">{Encode(supportingText)}</td></tr>";
        var html = $"""
            <!doctype html><html lang="en"><head><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#eef2ee;font-family:Arial,Helvetica,sans-serif;color:#1d2e25">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0">{safeMessage}</div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="width:100%;background:#eef2ee">
                <tr><td align="center" style="padding:36px 12px">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px;background:#ffffff;border:1px solid #dce5de;border-radius:22px;overflow:hidden;box-shadow:0 14px 40px rgba(18,55,42,.08)">
                    <tr><td style="padding:27px 34px;background:#12372a">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr>
                        <td><table role="presentation" cellspacing="0" cellpadding="0"><tr><td align="center" style="width:42px;height:42px;border-radius:12px;background:#f4c95d;color:#12372a;font-size:25px;font-weight:900">G</td><td style="padding-left:12px;color:#ffffff;font-size:24px;font-weight:800;letter-spacing:-.7px">Giddy<span style="color:#f4c95d">Edu</span></td></tr></table></td>
                        <td align="right" style="color:#b8d0c1;font-size:10px;font-weight:700;letter-spacing:1.4px;text-transform:uppercase">Secure account service</td>
                      </tr></table>
                    </td></tr>
                    <tr><td style="padding:42px 48px 10px;color:#2a6d50;font-size:11px;line-height:16px;font-weight:800;letter-spacing:1.7px;text-transform:uppercase">GiddyEdu notification</td></tr>
                    <tr><td style="padding:0 48px 16px;color:#17271f;font-size:30px;line-height:37px;font-weight:800;letter-spacing:-.8px">{safeHeading}</td></tr>
                    <tr><td style="padding:0 48px 28px;color:#52625a;font-size:16px;line-height:26px">{safeMessage}</td></tr>
                    {code}{action}{supporting}
                    <tr><td style="padding:24px 48px;background:#f8faf8;border-top:1px solid #e3e9e4">
                      <p style="margin:0 0 8px;color:#35473d;font-size:13px;line-height:20px;font-weight:700">Security reminder</p>
                      <p style="margin:0;color:#718078;font-size:12px;line-height:19px">GiddyEdu will never ask you to share a password or verification code by email, phone or message.</p>
                    </td></tr>
                  </table>
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px"><tr><td align="center" style="padding:22px 24px 0;color:#7a8981;font-size:11px;line-height:18px">GiddyEdu · One connected operating platform for modern schools<br>This automated email was sent because of activity on a GiddyEdu account.</td></tr></table>
                </td></tr>
              </table>
            </body></html>
            """;
        var text = $"GiddyEdu\n\n{heading}\n\n{message}{(verificationCode is null ? string.Empty : $"\n\nVerification code: {verificationCode}")}{(actionUrl is null ? string.Empty : $"\n\n{actionLabel}: {actionUrl}")}{(supportingText is null ? string.Empty : $"\n\n{supportingText}")}\n\nSecurity reminder: GiddyEdu will never ask you to share a password or verification code.";
        return new(html, text);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
