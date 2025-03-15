using Mirabel.CallCenter.NotificationHub;
using Mirabel.CallCenter.Common;

namespace program_settings
{
    public class Program()
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

             Configuration.SetConfiguration(builder.Configuration);
            // Add services to the container.

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp", builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                           //.AllowCredentials();
                });
            });

            builder.Services.AddControllers();
            builder.Services.AddSignalR();


            var app = builder.Build();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseCors("AllowReactApp");

            app.MapHub<Notifier>("/Notifier");

            app.MapControllers();


            app.MapGet("/", async (HttpContext context) =>
            {
                context.Response.ContentType = "text/plain"; // Optional: to ensure plain text content type
                await context.Response.WriteAsync("Hello, World!"); // Write the message to the response body
            });

            app.Run();


        }


    }
}
