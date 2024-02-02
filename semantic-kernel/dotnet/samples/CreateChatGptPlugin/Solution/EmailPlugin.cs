using System.ComponentModel;
using Microsoft.SemanticKernel;
namespace CreateChatGptPlugin;
public class EmailPlugin
{
    [KernelFunction, Description("Given an e-mail and message body, send an email")]
    public string SendEmail(
        [Description("The body of the email message to send.")] string input,
        [Description("The email address to send email to.")] string email_address) =>

        $"Sent email to: {email_address}. Body: {input}";

    [KernelFunction, Description("Given a name, find email address")]
    public string GetEmailAddress(
        [Description("The name of the person whose email address needs to be found.")] string input
        )
    {
        return input switch
        {
            "Jane" => "janedoe4321@example.com",
            "Paul" => "paulsmith5678@example.com",
            "Mary" => "maryjones8765@example.com",
            _ => "johndoe1234@example.com",
        };
    }
}
