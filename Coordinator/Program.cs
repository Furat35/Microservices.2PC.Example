using Coordinator.Models.Contexts;
using Coordinator.Services;
using Coordinator.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<TwoPhaseCommitContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("SQLServer")));
builder.Services.AddHttpClient("OrderAPI", configure => configure.BaseAddress = new("https://localhost:7210/"));
builder.Services.AddHttpClient("PaymentAPI", configure => configure.BaseAddress = new("https://localhost:7218/"));
builder.Services.AddHttpClient("StockAPI", configure => configure.BaseAddress = new("https://localhost:7211/"));
builder.Services.AddScoped<ITransactionService, TransactionService>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/create-order-transaction", async (ITransactionService transactionService) =>
{
    //Phase 1 - Prepare
    var transactionId = await transactionService.CreateTransactionAsync();
    await transactionService.PrepareServiceAsync(transactionId);
    bool transactionState = await transactionService.CheckReadyServicesAsync(transactionId);
    if (transactionState)
    {
        //Phase 2 - Commit
        await transactionService.CommitAsync(transactionId);
        transactionState = await transactionService.CheckTransactionStateServicesAsync(transactionId);
    }
    if(!transactionState)
        await transactionService.RollbackAsync(transactionId);
});

app.Run();
