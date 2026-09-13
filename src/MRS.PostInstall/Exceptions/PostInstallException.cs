namespace MRS.PostInstall.Exceptions;

/// <summary>Error controlado de <c>MRS.PostInstall</c> (P18): mismo espíritu que <c>IsoEngineException</c>/<c>RemovalEngineException</c> en los otros motores.</summary>
public sealed class PostInstallException : Exception
{
    public PostInstallException(string message) : base(message) { }

    public PostInstallException(string message, Exception innerException) : base(message, innerException) { }
}
