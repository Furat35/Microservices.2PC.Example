using Coordinator.Enums;

namespace Coordinator.Models
{
    public record NodeState(Guid TransactionId)
    {
        public Guid Id { get; set; }
    
        /// <summary>
        /// 1. aşamanın durumu
        /// </summary>
        public ReadyType IsReady { get; set; }

        /// <summary>
        /// 2. aşamanın başarılı gerçekleşip gerçekleşmediği 
        /// </summary>
        public TransactionState TransactionState { get; set; }

        public Node Node { get; set; }
    }
}
