#if !UNITY_WEBGL
using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace NT.Core.Net
{
    /// <summary>
    /// Configuration options for TLS/SSL connections.
    /// </summary>
    public class TlsOptions
    {
        /// <summary>
        /// TLS protocol version to use. Default: Tls12.
        /// </summary>
        public SslProtocols Protocols { get; set; } = SslProtocols.Tls12;

        /// <summary>
        /// Optional certificate validation callback.
        /// If null, uses default OS certificate validation.
        /// Return true to accept certificate, false to reject.
        /// </summary>
        public RemoteCertificateValidationCallback CertificateValidator { get; set; }

        private X509Certificate2 _clientCertificate;

        /// <summary>
        /// Optional client certificate for mutual TLS authentication.
        /// </summary>
        public X509Certificate2 ClientCertificate
        {
            get => _clientCertificate;
            set
            {
                if (value != null)
                {
                    // Check if certificate has a private key (required for client authentication)
                    if (!value.HasPrivateKey)
                    {
                        throw new ArgumentException("[TlsOptions] Client certificate must have a private key for mutual TLS authentication.", nameof(ClientCertificate));
                    }

                    // Check if certificate is expired
                    if (DateTime.Now > value.NotAfter)
                    {
                        throw new ArgumentException($"[TlsOptions] Client certificate has expired. Expired on: {value.NotAfter}", nameof(ClientCertificate));
                    }

                    // Check if certificate is not yet valid
                    if (DateTime.Now < value.NotBefore)
                    {
                        throw new ArgumentException($"[TlsOptions] Client certificate is not yet valid. Valid from: {value.NotBefore}", nameof(ClientCertificate));
                    }
                }
                _clientCertificate = value;
            }
        }

        /// <summary>
        /// Whether to check certificate revocation. Default: true.
        /// </summary>
        public bool CheckCertificateRevocation { get; set; } = true;

        /// <summary>
        /// Creates default TlsOptions (TLS 1.2, standard certificate validation).
        /// </summary>
        public static TlsOptions Default => new TlsOptions();
    }
}
#endif
