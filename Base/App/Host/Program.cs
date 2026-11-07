using VSA.Host;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddVsa();

WebApplication app = builder.Build();
app.MapVsaRequests();
app.Run();
