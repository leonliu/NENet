#if !UNITY_WEBGL
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

namespace NT.Core.Net
{
    /// <summary>
    /// TLS/SSL transport layer. Extends Transport with SslStream wrapper for encrypted communication.
    /// </summary>
    public class SecureTransport : Transport
    {
        private readonly TlsOptions _options;
        private SslStream _sslStream;
        private string _targetHost;

        /// <summary>
        /// Creates a secure transport with the specified TLS options.
        /// </summary>
        public SecureTransport(TlsOptions options)
        {
            _options = options ?? TlsOptions.Default;
        }

        /// <summary>
        /// Returns the SslStream for this secure transport.
        /// Overrides the base Transport.GetStream() to provide the TLS-wrapped stream.
        /// </summary>
        protected override Stream GetStream()
        {
            return _sslStream ?? base.GetStream();
        }

        /// <summary>
        /// Connects to the specified host and port using TLS/SSL.
        /// Performs TCP handshake first, then TLS handshake.
        /// </summary>
        public new void Connect(string host, int port)
        {
            _targetHost = host;

            // Perform TCP handshake via base class
            base.Connect(host, port);

            // Get the raw NetworkStream and wrap it with SslStream
            NetworkStream netStream = Socket.GetStream();
            _sslStream = CreateSslStream(netStream);

            // Perform TLS handshake
            try
            {
                PerformTlsHandshake(_sslStream, _targetHost);

                Debug.Log($"[SecureTransport] TLS handshake successful: {_targetHost}, " +
                         $"protocol: {_sslStream.SslProtocol}, cipher: {_sslStream.CipherAlgorithm}");
            }
            catch (AuthenticationException e)
            {
                Debug.LogError($"[SecureTransport] TLS authentication failed: {e.Message}");
                throw;
            }
            catch (IOException e)
            {
                Debug.LogError($"[SecureTransport] TLS handshake I/O error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates and configures the SslStream.
        /// </summary>
        private SslStream CreateSslStream(NetworkStream innerStream)
        {
            return new SslStream(
                innerStream,
                leaveInnerStreamOpen: false,  // Close inner stream when SslStream is closed
                new RemoteCertificateValidationCallback(ValidateCertificate)
            );
        }

        /// <summary>
        /// Performs the TLS handshake as a client.
        /// </summary>
        private void PerformTlsHandshake(SslStream sslStream, string targetHost)
        {
            // Prepare client certificates if provided
            X509CertificateCollection certificates = null;
            if (_options.ClientCertificate != null)
            {
                certificates = new X509CertificateCollection { _options.ClientCertificate };
            }

            sslStream.AuthenticateAsClient(
                targetHost,
                certificates,
                _options.Protocols,
                _options.CheckCertificateRevocation
            );
        }

        /// <summary>
        /// Certificate validation callback that delegates to user-provided validator.
        /// </summary>
        private bool ValidateCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            // If user provided custom validator, use it
            if (_options.CertificateValidator != null)
            {
                return _options.CertificateValidator(sender, certificate, chain, sslPolicyErrors);
            }

            // Default validation: accept only valid certificates
            bool isValid = sslPolicyErrors == SslPolicyErrors.None;

            if (!isValid)
            {
                Debug.LogWarning($"[SecureTransport] Certificate validation failed: {sslPolicyErrors}");
            }

            return isValid;
        }
    }
}
#endif
