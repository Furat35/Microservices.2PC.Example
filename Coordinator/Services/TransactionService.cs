using Coordinator.Models;
using Coordinator.Models.Contexts;
using Coordinator.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Coordinator.Services
{
    public class TransactionService(TwoPhaseCommitContext _context, IHttpClientFactory _httpClientFactory) : ITransactionService
    {
        HttpClient _orderHttpClient = _httpClientFactory.CreateClient("OrderAPI");
        HttpClient _paymentHttpClient = _httpClientFactory.CreateClient("PaymentAPI");
        HttpClient _stockHttpClient = _httpClientFactory.CreateClient("StockAPI");

        public async Task<Guid> CreateTransactionAsync()
        {
            Guid transactionId = Guid.NewGuid();
            var nodes = await _context.Nodes.ToListAsync();
            nodes.ForEach(node => node.NodeStates = new List<NodeState>
            {
                new(transactionId)
                {
                    IsReady = Enums.ReadyType.Pending,
                    TransactionState = Enums.TransactionState.Pending
                }
            });
            await _context.SaveChangesAsync();
            return transactionId;
        }

        public async Task PrepareServiceAsync(Guid transactionId)
        {
            var transactionNodes = await _context.NodeStates
                .Include(ns => ns.Node)
                .Where(ns => ns.TransactionId == transactionId)
                .ToListAsync();
            foreach (var transactionNode in transactionNodes)
            {
                try
                {
                    var response = transactionNode.Node.Name switch
                    {
                        "OrderAPI" => _orderHttpClient.GetAsync("ready"),
                        "PaymentAPI" => _paymentHttpClient.GetAsync("ready"),
                        "StockAPI" => _stockHttpClient.GetAsync("ready")
                    };

                    var result = bool.Parse(await response.Result.Content.ReadAsStringAsync());
                    transactionNode.IsReady = result ? Enums.ReadyType.Ready : Enums.ReadyType.Unready;
                }
                catch (Exception)
                {
                    transactionNode.IsReady = Enums.ReadyType.Unready;
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task<bool> CheckReadyServicesAsync(Guid transactionId)
            => (await _context.NodeStates
                .Where(ns => ns.TransactionId == transactionId)
                .ToListAsync()).TrueForAll(ns => ns.IsReady == Enums.ReadyType.Ready);

        public async Task CommitAsync(Guid transactionId)
        {
            var transactionNodes = await _context.NodeStates
                .Where(ns => ns.TransactionId == transactionId)
                .Include(ns => ns.Node)
                .ToListAsync();
            foreach (var transactionNode in transactionNodes)
            {
                try
                {
                    var response = transactionNode.Node.Name switch
                    {
                        "OrderAPI" => _orderHttpClient.GetAsync("commit"),
                        "PaymentAPI" => _paymentHttpClient.GetAsync("commit"),
                        "StockAPI" => _stockHttpClient.GetAsync("commit")
                    };

                    var result = bool.Parse(await response.Result.Content.ReadAsStringAsync());
                    transactionNode.TransactionState = result ? Enums.TransactionState.Done : Enums.TransactionState.Abort;
                }
                catch (Exception)
                {
                    transactionNode.TransactionState = Enums.TransactionState.Abort;
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task<bool> CheckTransactionStateServicesAsync(Guid transactionId)
            => (await _context.NodeStates
           .Where(ns => ns.TransactionId == transactionId)
           .ToListAsync()).TrueForAll(ns => ns.TransactionState == Enums.TransactionState.Done);


        public async Task RollbackAsync(Guid transactionId)
        {
            var transactionNodes = await _context.NodeStates
                .Where(ns => ns.TransactionId == transactionId)
                .Include(ns => ns.Node)
                .ToListAsync();

            foreach (var transactionNode in transactionNodes)
            {
                try
                {
                    if (transactionNode.TransactionState == Enums.TransactionState.Done)
                    {
                        _ = transactionNode.Node.Name switch
                        {
                            "OrderAPI" => _orderHttpClient.GetAsync("rollback"),
                            "PaymentAPI" => _paymentHttpClient.GetAsync("rollback"),
                            "StockAPI" => _stockHttpClient.GetAsync("rollback")
                        };
                    }
                    transactionNode.TransactionState = Enums.TransactionState.Abort;
                }
                catch (Exception)
                {
                    transactionNode.TransactionState = Enums.TransactionState.Abort;
                }

                await _context.SaveChangesAsync();
            }
        }
    }
}
