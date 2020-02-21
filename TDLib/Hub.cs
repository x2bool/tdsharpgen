using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TdLib
{
    public class Hub
    {
        private readonly CancellationTokenSource _cts;
        private readonly ILogger<Hub> _logger;
        private readonly Client _client;
        
        public event EventHandler<TdApi.Object> Received;
        
        public Hub(
            ILogger<Hub> logger,
            Client client
            )
        {
            _cts = new CancellationTokenSource();
            _logger = logger;
            _client = client;
        }

        public void Start()
        {
            var ct = _cts.Token;
            while (!ct.IsCancellationRequested)
            {
                var data = _client.Receive(10.0);
                if (!string.IsNullOrEmpty(data))
                {
                    _logger.LogDebug(data);
                    var structure = JsonConvert.DeserializeObject<TdApi.Object>(data, new Converter());
                    Received?.Invoke(this, structure);
                }
            }
        }

        public void Stop()
        {
            _cts.Cancel();
        }
    }
}