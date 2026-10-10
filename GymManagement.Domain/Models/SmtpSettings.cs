public class SmtpSettings
{
    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 587;

    public string User { get; set; } = "";

    public string Password { get; set; } = "";

    public string FromEmail { get; set; } = "";

    public string FromName { get; set; } = "Coco Republic";
}