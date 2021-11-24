namespace Wasari.Crunchyroll.API
{
    internal class ApiSignature
    {
        public string Bucket { get; init; }

        public string Policy { get; init; }

        public string Signature { get; init; }

        public string KeyPairId { get; init; }
    }
}