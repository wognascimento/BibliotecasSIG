using System.IO.Compression;
using System.Text.Json;

namespace BibliotecasSIG
{
    public class UpdateInfo
    {
        public string currentVersion { get; set; }
        public string updateVersion { get; set; }
        public string updateUrl { get; set; }
        public string[] changelog { get; set; }
        public string releaseDate { get; set; }
        public string minimumCompatibleVersion { get; set; }
    }

    public class UpdateChecker
    {
        private readonly string _updateInfoUrl;
        private readonly string _currentVersion;

        public UpdateChecker()
        {
        }

        public UpdateChecker(string updateInfoUrl, string currentVersion)
        {
            _updateInfoUrl = updateInfoUrl;
            _currentVersion = currentVersion;
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                var response = await client.GetStringAsync(_updateInfoUrl);
                var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(response);

                if (IsUpdateAvailable(_currentVersion, updateInfo.updateVersion))
                    return updateInfo;

                return null;
            }
            catch (HttpRequestException)
            {
                throw;
            }
        }

        private static bool IsUpdateAvailable(string currentVersion, string newVersion)
        {
            var current = new Version(currentVersion);
            var latest = new Version(newVersion);

            return latest > current;
        }

        public async Task<bool> DownloadUpdateAsync(string downloadUrl, string destinationPath)
        {
            try
            {
                using var client = new HttpClient();
                var fileBytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(destinationPath, fileBytes);
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<bool> DownloadUpdateAsync(string downloadUrl, string destinationPath, IProgress<double> progress = null)
        {
            try
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1 && progress != null;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var totalRead = 0L;
                var buffer = new byte[8192];
                var isMoreToRead = true;

                while (isMoreToRead)
                {
                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        isMoreToRead = false;
                        progress?.Report(100);
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (canReportProgress)
                    {
                        double percent = Math.Round((double)totalRead / totalBytes * 100, 2);
                        progress.Report(percent);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<bool> ExtrairZipComProgressoAsync(string caminhoZip, IProgress<double> progress = null)
        {
            try
            {
                // Obtém a pasta onde o ZIP está localizado (ex: "Upload")
                string pastaUpload = Path.GetDirectoryName(caminhoZip);
                if (string.IsNullOrEmpty(pastaUpload))
                    throw new Exception("Pasta do arquivo zip não encontrada.");

                // Obtém a pasta pai, onde será extraído o conteúdo
                string pastaPai = Directory.GetParent(pastaUpload)?.FullName;
                if (string.IsNullOrEmpty(pastaPai))
                    throw new Exception("Pasta pai não encontrada.");

                // Lista de arquivos que você NÃO quer extrair
                HashSet<string> arquivosIgnorar = new()
                {
                    "BibliotecasSIG.dll",
                    "BibliotecasSIG.pdb",
                    "Update.deps.json",
                    "Update.dll",
                    "Update.exe",
                    "Update.pdb",
                    "Update.runtimeconfig.json"
                };

                // Abre o arquivo zip para leitura
                using ZipArchive archive = ZipFile.OpenRead(caminhoZip);
                // Filtra as entradas para excluir os arquivos específicos
                var arquivosParaExtrair = archive.Entries
                    .Where(entry => !arquivosIgnorar.Contains(entry.Name))
                    .ToList();

                //int totalEntries = archive.Entries.Count;
                int totalEntries = arquivosParaExtrair.Count;
                int processedEntries = 0;

                // Itera por cada entrada do ZIP
                //foreach (var entry in archive.Entries)
                foreach (var entry in arquivosParaExtrair)
                {
                    // Destino: combina a pasta pai com o nome completo da entrada
                    string destino = Path.Combine(pastaPai, entry.FullName);
                    

                    // Se a entrada for um diretório, cria-o; caso contrário, extrai o arquivo
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destino);
                    }
                    else
                    {
                        // Cria o diretório destino, se não existir
                        Directory.CreateDirectory(Path.GetDirectoryName(destino));
                        // Extrai o arquivo, sobrescrevendo se já existir
                        entry.ExtractToFile(destino, overwrite: true);
                    }

                    processedEntries++;
                    // Calcula a porcentagem e reporta o progresso
                    double percentual = processedEntries * 100.0 / totalEntries;
                    progress?.Report(percentual);

                    // Opcional: aguardar um pequeno delay para simular trabalho assíncrono
                    await Task.Delay(10);
                }
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

    }

}

