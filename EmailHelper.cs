using System;
using System.Net;
using System.Net.Mail;
using System.Configuration;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Şifre sıfırlama e-postası gönderim servisi.
    /// SMTP ayarları App.config'den okunur; ayar yoksa sessiz başarısız olur.
    /// </summary>
    public static class EmailHelper
    {
        public static void SendPasswordResetEmail(string toAddress, string fullName)
        {
            try
            {
                // App.config'den SMTP ayarları
                string smtpHost  = ConfigurationManager.AppSettings["SmtpHost"]  ?? "smtp.gmail.com";
                string smtpUser  = ConfigurationManager.AppSettings["SmtpUser"]  ?? "";
                string smtpPass  = ConfigurationManager.AppSettings["SmtpPass"]  ?? "";
                int    smtpPort  = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out int p) ? p : 587;

                if (string.IsNullOrWhiteSpace(smtpUser)) return; // Credential yoksa çık

                var mail = new MailMessage
                {
                    From       = new MailAddress(smtpUser, "TUSAŞ Gelişim Pusulası"),
                    Subject    = "Şifre Sıfırlama Talebi – TUSAŞ Gelişim Pusulası",
                    IsBodyHtml = true,
                    Body       = BuildEmailBody(fullName)
                };
                mail.To.Add(toAddress);

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl             = true;
                    client.Credentials           = new NetworkCredential(smtpUser, smtpPass);
                    client.Timeout               = 10000;
                    client.DeliveryMethod        = SmtpDeliveryMethod.Network;
                    client.Send(mail);
                }
            }
            catch
            {
                // Sessizce geç — kullanıcıya her durumda "mail gönderildi" göstereceğiz
            }
        }

        private static string BuildEmailBody(string fullName)
        {
            return $@"
<html>
<body style='font-family:Segoe UI,Arial,sans-serif; background:#f0f4f8; padding:0; margin:0;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f0f4f8;'>
    <tr><td align='center' style='padding:40px 20px;'>
      <table width='560' cellpadding='0' cellspacing='0' style='background:#ffffff; border-radius:4px;'>
        <tr>
          <td style='background:#0E1A32; padding:24px 32px;'>
            <span style='color:#ffffff; font-size:20px; font-weight:bold; letter-spacing:1px;'>TUSAŞ</span>
            <span style='color:#a0b4cc; font-size:13px; margin-left:10px;'>Gelişim Pusulası</span>
          </td>
        </tr>
        <tr>
          <td style='padding:36px 32px;'>
            <p style='color:#0e1a32; font-size:22px; font-weight:bold; margin:0 0 16px 0;'>Şifre Sıfırlama Talebi</p>
            <p style='color:#333; font-size:15px; line-height:1.6;'>Merhaba <strong>{fullName}</strong>,</p>
            <p style='color:#333; font-size:15px; line-height:1.6;'>
              TUSAŞ Gelişim Pusulası sistemine yönelik bir şifre sıfırlama talebinde bulunulmuştur.
            </p>
            <p style='color:#333; font-size:15px; line-height:1.6;'>
              Şifrenizi sıfırlamak için lütfen TUSAŞ İK departmanıyla iletişime geçiniz
              ya da sistem yöneticinizden yardım talep ediniz.
            </p>
            <p style='color:#666; font-size:13px; margin-top:28px; border-top:1px solid #eee; padding-top:16px;'>
              Bu e-posta TUSAŞ Gelişim Pusulası sistemi tarafından otomatik olarak iletilmiştir.
              Lütfen yanıtlamayınız.
            </p>
          </td>
        </tr>
        <tr>
          <td style='background:#f4f6f8; padding:16px 32px; border-top:3px solid #E30613;'>
            <span style='color:#999; font-size:12px;'>TUSAŞ Gelişim Pusulası v2.0 &copy; 2026</span>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
        }
    }
}
