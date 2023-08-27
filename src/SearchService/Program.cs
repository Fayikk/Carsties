using System.Net;
using Contracts;
using MassTransit;
using Polly;
using Polly.Extensions.Http;
using SearchService;
using SearchService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<AuctionSvcHttpClient>().AddPolicyHandler(GetPolicy());

     builder.Services.AddMassTransit( x => {
        x.AddConsumersFromNamespaceContaining<AuctionCreatedConsumer>();
       x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("search",false));
        x.UsingRabbitMq((context,cfg) => 
        {
            cfg.ReceiveEndpoint("search-auction-created",e => {
                e.UseMessageRetry(r => r.Interval(5,5));
                e.ConfigureConsumer<AuctionCreatedConsumer>(context);
            
            }) ;  
            cfg.ConfigureEndpoints(context);
        });
    });



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}



app.UseAuthorization();



app.MapControllers();
app.Lifetime.ApplicationStarted.Register(async () => {
    try
    {
        await DbInitializer.InitDb(app);
    }
    catch (System.Exception ex)
    {
        
        Console.WriteLine(ex.Message);  
    }
});



app.Run();
static IAsyncPolicy<HttpResponseMessage> GetPolicy() 
    => HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == HttpStatusCode.NotFound)
        .WaitAndRetryForeverAsync(_ => TimeSpan.FromSeconds(3));