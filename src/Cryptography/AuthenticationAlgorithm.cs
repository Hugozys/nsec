using System;
using System.Diagnostics;
using static Interop.Libsodium;

namespace NSec.Cryptography
{
    public abstract class AuthenticationAlgorithm : Algorithm
    {
        private readonly int _defaultKeySize;
        private readonly int _defaultMacSize;
        private readonly int _maxKeySize;
        private readonly int _maxNonceSize;
        private readonly int _maxMacSize;
        private readonly int _minKeySize;
        private readonly int _minNonceSize;
        private readonly int _minMacSize;

        internal AuthenticationAlgorithm(
            int minKeySize,
            int defaultKeySize,
            int maxKeySize,
            int minNonceSize,
            int maxNonceSize,
            int minMacSize,
            int defaultMacSize,
            int maxMacSize)
        {
            Debug.Assert(minKeySize > 0);
            Debug.Assert(defaultKeySize >= minKeySize);
            Debug.Assert(maxKeySize >= defaultKeySize);
            Debug.Assert(minNonceSize >= 0);
            Debug.Assert(maxNonceSize >= minNonceSize);
            Debug.Assert(minMacSize > 0);
            Debug.Assert(defaultMacSize >= minMacSize);
            Debug.Assert(maxMacSize >= defaultMacSize);

            _minKeySize = minKeySize;
            _defaultKeySize = defaultKeySize;
            _maxKeySize = maxKeySize;
            _minNonceSize = minNonceSize;
            _maxNonceSize = maxNonceSize;
            _minMacSize = minMacSize;
            _defaultMacSize = defaultMacSize;
            _maxMacSize = maxMacSize;
        }

        public int DefaultKeySize => _defaultKeySize;

        public int DefaultMacSize => _defaultMacSize;

        public int MaxKeySize => _maxKeySize;

        public int MaxNonceSize => _maxNonceSize;

        public int MaxMacSize => _maxMacSize;

        public int MinKeySize => _minKeySize;

        public int MinNonceSize => _minNonceSize;

        public int MinMacSize => _minMacSize;

        public byte[] Sign(
            Key key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> data)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Algorithm != this)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(key));
            if (nonce.Length < _minNonceSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(nonce));
            if (nonce.Length > _maxNonceSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(nonce));

            byte[] mac = new byte[_defaultMacSize];
            SignCore(key, nonce, data, mac);
            return mac;
        }

        public byte[] Sign(
            Key key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> data,
            int macSize)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Algorithm != this)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(key));
            if (nonce.Length < _minNonceSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(nonce));
            if (nonce.Length > _maxNonceSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(nonce));
            if (macSize < _minMacSize)
                throw new ArgumentOutOfRangeException(nameof(macSize));
            if (macSize > _maxMacSize)
                throw new ArgumentOutOfRangeException(nameof(macSize));

            byte[] mac = new byte[macSize];
            SignCore(key, nonce, data, mac);
            return mac;
        }

        public void Sign(
            Key key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> data,
            Span<byte> mac)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Algorithm != this)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(key));
            if (nonce.Length < _minNonceSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(nonce));
            if (nonce.Length > _maxNonceSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(nonce));
            if (mac.Length < _minMacSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(mac));
            if (mac.Length > _maxMacSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(mac));

            SignCore(key, nonce, data, mac);
        }

        public bool TryVerify(
            Key key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> data,
            ReadOnlySpan<byte> mac)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Algorithm != this)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(key));
            if (nonce.Length < _minNonceSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(nonce));
            if (nonce.Length > _maxNonceSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(nonce));
            if (mac.Length < _minMacSize)
                return false;
            if (mac.Length > _maxMacSize)
                return false;

            // crypto_auth_hmacsha{256,512}_verify does not support truncated
            // HMACs, so we calculate the MAC ourselves and call sodium_memcmp
            // to compare the expected MAC with the actual MAC.

            // SignCore can output a truncated MAC. However, truncation requires
            // a copy. So we provide an array with the default length and
            // compare only the initial 'mac.Length' bytes.

            byte[] result = new byte[_defaultMacSize]; // TODO: avoid placing sensitive data in managed memory
            SignCore(key, nonce, data, result);

            int error = sodium_memcmp(result, ref mac.DangerousGetPinnableReference(), (IntPtr)mac.Length);
            return error == 0;
        }

        public void Verify(
            Key key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> data,
            ReadOnlySpan<byte> mac)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Algorithm != this)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(key));
            if (nonce.Length < _minNonceSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(nonce));
            if (nonce.Length > _maxNonceSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(nonce));
            if (mac.Length < _minMacSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(mac));
            if (mac.Length > _maxMacSize)
                throw new ArgumentException(Error.ArgumentExceptionMessage, nameof(mac));

            // crypto_auth_hmacsha{256,512}_verify does not support truncated
            // HMACs, so we calculate the MAC ourselves and call sodium_memcmp
            // to compare the expected MAC with the actual MAC.

            // SignCore can output a truncated MAC. However, truncation requires
            // a copy. So we provide an array with the default length and
            // compare only the initial 'mac.Length' bytes.

            byte[] result = new byte[_defaultMacSize]; // TODO: avoid placing sensitive data in managed memory
            SignCore(key, nonce, data, result);

            int error = sodium_memcmp(result, ref mac.DangerousGetPinnableReference(), (IntPtr)mac.Length);
            if (error != 0)
            {
                throw new CryptographicException();
            }
        }

        internal abstract void SignCore(
            Key key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> data,
            Span<byte> mac);
    }
}