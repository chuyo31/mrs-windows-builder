namespace MRS.ISOEngine.TreeCopy;

/// <summary>Copia recursiva simple de un directorio a otro (sobrescribiendo). Compartida por <see cref="IsoTreeCopier"/> y el pipeline de P19 (fusión de <c>$OEM$</c>) para no duplicarla.</summary>
public static class DirectoryCopyHelper
{
    public static int CopyAll(string sourceDir, string destinationDir, CancellationToken cancellationToken = default)
    {
        var count = 0;
        Directory.CreateDirectory(destinationDir);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationFile = Path.Combine(destinationDir, Path.GetFileName(sourceFile));
            File.Copy(sourceFile, destinationFile, overwrite: true);
            count++;
        }

        foreach (var sourceSubDir in Directory.EnumerateDirectories(sourceDir))
        {
            var destinationSubDir = Path.Combine(destinationDir, Path.GetFileName(sourceSubDir));
            count += CopyAll(sourceSubDir, destinationSubDir, cancellationToken);
        }

        return count;
    }
}
