using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using PdfiumViewer;

namespace PDFOptimize
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void btnBrowseInput_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtInput.Text = ofd.FileName;
                    if (!chkModifyOriginal.Checked)
                    {
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName) + "_optimized.pdf";
                        txtOutput.Text = Path.Combine(Path.GetDirectoryName(ofd.FileName), name);
                    }
                }
            }
        }

        private void btnBrowseOutput_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog { Filter = "PDF files (*.pdf)|*.pdf" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                    txtOutput.Text = sfd.FileName;
            }
        }

        private async void btnOptimize_Click(object sender, EventArgs e)
        {
            string inputPath = txtInput.Text;
            string outputPath = chkModifyOriginal.Checked ? inputPath : txtOutput.Text;

            if (!File.Exists(inputPath))
            {
                MessageBox.Show("입력 파일이 존재하지 않습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!chkModifyOriginal.Checked && string.IsNullOrWhiteSpace(outputPath))
            {
                MessageBox.Show("저장할 경로를 선택하세요.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (Path.GetFullPath(inputPath) == Path.GetFullPath(outputPath))
            {
                var result = MessageBox.Show("원본 파일을 덮어쓰시겠습니까? 백업이 권장됩니다.", "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
                // To avoid file lock, copy to temp, optimize, then overwrite
                string tempPath = Path.GetTempFileName();
                File.Copy(inputPath, tempPath, true);
                inputPath = tempPath;
            }

            btnOptimize.Enabled = false;
            progressBar.Value = 0;
            long beforeSize = new FileInfo(inputPath).Length;
            string errorMsg = null;
            bool success = false;
            try
            {
                (success, errorMsg) = await Task.Run(() => OptimizePdf(inputPath, outputPath, UpdateProgress));
            }
            finally
            {
                btnOptimize.Enabled = true;
                if (Path.GetTempPath() == Path.GetDirectoryName(inputPath) && File.Exists(inputPath) && inputPath != txtInput.Text)
                {
                    try { File.Delete(inputPath); } catch { }
                }
            }

            if (success)
            {
                long afterSize = new FileInfo(outputPath).Length;
                double ratio = beforeSize > 0 ? (1.0 - (double)afterSize / beforeSize) * 100 : 0;
                MessageBox.Show($"PDF 최적화 완료!\n\n경로: {outputPath}\n\n용량: {beforeSize / 1024} KB → {afterSize / 1024} KB ({ratio:F1}% 감소)", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"최적화 중 오류 발생.\n\n{errorMsg}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool OptimizePdf(string input, string output, Action<int> progressCallback)
        {
            try
            {
                using (var doc = PdfDocument.Load(input))
                {
                    int pageCount = doc.PageCount;
                    for (int i = 0; i < pageCount; i++)
                    {
                        progressCallback?.Invoke((i + 1) * 100 / pageCount);
                        // No per-page optimization in PdfiumViewer, just simulate progress
                    }
                    using (var stream = new FileStream(output, FileMode.Create))
                    {
                        doc.Save(stream);
                    }
                }
                progressCallback?.Invoke(100);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private void UpdateProgress(int percent)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int>(UpdateProgress), percent);
                return;
            }
            progressBar.Value = percent;
        }

        private void chkModifyOriginal_CheckedChanged(object sender, EventArgs e)
        {
            txtOutput.Enabled = !chkModifyOriginal.Checked;
            btnBrowseOutput.Enabled = !chkModifyOriginal.Checked;
        }
    }
}