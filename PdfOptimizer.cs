using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PDFOptimize
{
    public static class PdfOptimizer
    {
        // Ghostscript를 이용한 PDF 최적화 (스트림 방식, 대용량 지원)
        public static (bool, string) OptimizeWithGhostscript(string input, string output)
        {
            string gsPath = "gswin64c.exe"; // 환경에 따라 경로 조정 필요
            if (!File.Exists(input))
                return (false, "입력 파일이 존재하지 않습니다.");
            string args = $"-dNOPAUSE -dBATCH -dSAFER -sDEVICE=pdfwrite -dPDFSETTINGS=/ebook -sOutputFile=\"{output}\" \"{input}\"";
            try
            {
                var psi = new ProcessStartInfo(gsPath, args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        string err = proc.StandardError.ReadToEnd();
                        return (false, $"Ghostscript 오류: {err}");
                    }
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Ghostscript 실행 오류: {ex.Message}");
            }
        }

        // PDF 분할 (페이지 수 기준)
        public static List<string> SplitPdf(string input, int pagesPerFile, string outputDir)
        {
            var outputFiles = new List<string>();
            using (var doc = PdfiumViewer.PdfDocument.Load(input))
            {
                int totalPages = doc.PageCount;
                int fileIndex = 1;
                for (int i = 0; i < totalPages; i += pagesPerFile)
                {
                    int endPage = Math.Min(i + pagesPerFile, totalPages);
                    var pages = new List<int>();
                    for (int p = i; p < endPage; p++)
                        pages.Add(p);
                    string outPath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(input)}_part{fileIndex}.pdf");
                    using (var stream = new FileStream(outPath, FileMode.Create))
                    {
                        doc.Save(stream, pages);
                    }
                    outputFiles.Add(outPath);
                    fileIndex++;
                }
            }
            return outputFiles;
        }
    }
}
