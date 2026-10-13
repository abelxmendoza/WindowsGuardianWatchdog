using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog
{
    public class PipeClient : IDisposable
    {
        private NamedPipeClientStream? _pipeClient;
        private readonly string _pipeName;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _disposed = false;

        public event EventHandler<string>? MessageReceived;
        public event EventHandler<string>? ErrorOccurred;
        public bool IsConnected => _pipeClient?.IsConnected ?? false;

        public PipeClient(string pipeName = "WindowsGuardianWatchdog")
        {
            _pipeName = pipeName;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task<bool> ConnectAsync(int timeoutMs = 5000)
        {
            try
            {
                if (_pipeClient != null && _pipeClient.IsConnected)
                {
                    return true;
                }

                _pipeClient?.Dispose();
                _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                await _pipeClient.ConnectAsync(timeoutMs, _cancellationTokenSource.Token);
                
                OnMessageReceived("Connected to pipe server");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendMessageAsync(string message)
        {
            try
            {
                if (_pipeClient == null || !_pipeClient.IsConnected)
                {
                    OnErrorOccurred("Not connected to pipe server");
                    return false;
                }

                byte[] buffer = Encoding.UTF8.GetBytes(message);
                await _pipeClient.WriteAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token);
                await _pipeClient.FlushAsync(_cancellationTokenSource.Token);
                
                OnMessageReceived($"Sent: {message}");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Send failed: {ex.Message}");
                return false;
            }
        }

        public async Task<string?> ReceiveMessageAsync()
        {
            try
            {
                if (_pipeClient == null || !_pipeClient.IsConnected)
                {
                    OnErrorOccurred("Not connected to pipe server");
                    return null;
                }

                byte[] buffer = new byte[4096];
                int bytesRead = await _pipeClient.ReadAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token);
                
                if (bytesRead > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    OnMessageReceived($"Received: {message}");
                    return message;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Receive failed: {ex.Message}");
                return null;
            }
        }

        public void Disconnect()
        {
            try
            {
                _pipeClient?.Close();
                _pipeClient?.Dispose();
                _pipeClient = null;
                OnMessageReceived("Disconnected from pipe server");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Disconnect failed: {ex.Message}");
            }
        }

        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, message);
        }

        protected virtual void OnErrorOccurred(string error)
        {
            ErrorOccurred?.Invoke(this, error);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Cancel();
                Disconnect();
                _cancellationTokenSource.Dispose();
                _disposed = true;
            }
        }
    }
}

