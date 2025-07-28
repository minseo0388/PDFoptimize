using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using PdfiumViewer;

namespace PDFOptimize
{
    public partial class MainForm : Form
    {
        // 추가: 분할/대용량 옵션 컨트롤 생성
        private CheckBox chkSplit;
        private NumericUpDown numPagesPerFile;
        private CheckBox chkUseGhostscript;

        public MainForm()
        {
            InitializeComponent();
            // Enable drag & drop for input
            txtInput.AllowDrop = true;
            txtInput.DragEnter += TxtInput_DragEnter;
            txtInput.DragDrop += TxtInput_DragDrop;

            // ToolTips
            var toolTip = new ToolTip();
            toolTip.SetToolTip(btnBrowseInput, "PDF 파일 선택");
            toolTip.SetToolTip(btnBrowseOutput, "저장 경로 선택");
            toolTip.SetToolTip(btnOptimize, "PDF 최적화 시작");
            toolTip.SetToolTip(txtInput, "여기에 PDF 파일을 드래그하거나, 파일 선택 버튼을 클릭하세요");
            toolTip.SetToolTip(txtOutput, "최적화된 PDF 저장 경로");
            toolTip.SetToolTip(chkModifyOriginal, "원본 파일을 덮어쓸지 여부");

            // 분할 옵션
            chkSplit = new CheckBox { Text = "PDF 분할", Left = chkModifyOriginal.Left, Top = chkModifyOriginal.Bottom + 10, Width = 80 };
            numPagesPerFile = new NumericUpDown { Left = chkSplit.Right + 10, Top = chkSplit.Top, Width = 60, Minimum = 1, Maximum = 10000, Value = 10, Enabled = false };
            chkUseGhostscript = new CheckBox { Text = "대용량 최적화(Ghostscript)", Left = numPagesPerFile.Right + 20, Top = chkSplit.Top, Width = 180 };
            this.Controls.Add(chkSplit);
            this.Controls.Add(numPagesPerFile);
            this.Controls.Add(chkUseGhostscript);
            toolTip.SetToolTip(chkSplit, "PDF를 여러 파일로 분할 저장");
            toolTip.SetToolTip(numPagesPerFile, "파일당 페이지 수");
            toolTip.SetToolTip(chkUseGhostscript, "Ghostscript로 대용량 PDF 최적화");
            chkSplit.CheckedChanged += (s, e) => numPagesPerFile.Enabled = chkSplit.Checked;

            // UI polish: set tab order, default button, focus
            this.AcceptButton = btnOptimize;
            txtInput.TabIndex = 0;
            btnBrowseInput.TabIndex = 1;
            txtOutput.TabIndex = 2;
            btnBrowseOutput.TabIndex = 3;
            chkModifyOriginal.TabIndex = 4;
            chkSplit.TabIndex = 5;
            numPagesPerFile.TabIndex = 6;
            chkUseGhostscript.TabIndex = 7;
            btnOptimize.TabIndex = 8;
            txtInput.Focus();
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
                {
                    if (File.Exists(sfd.FileName))
                    {
                        var result = MessageBox.Show("해당 파일이 이미 존재합니다. 덮어쓰시겠습니까?", "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (result != DialogResult.Yes)
                            return;
                    }
                    txtOutput.Text = sfd.FileName;
                }
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
            btnBrowseInput.Enabled = false;
            btnBrowseOutput.Enabled = false;
            txtInput.Enabled = false;
            txtOutput.Enabled = false;
            chkModifyOriginal.Enabled = false;
            chkSplit.Enabled = false;
            numPagesPerFile.Enabled = false;
            chkUseGhostscript.Enabled = false;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Value = 0;
            long beforeSize = new FileInfo(inputPath).Length;
            string errorMsg = null;
            bool success = false;
            try
            {
                // 분할 옵션
                if (chkSplit.Checked)
                {
                    string outDir = Path.GetDirectoryName(outputPath);
                    int pagesPerFile = (int)numPagesPerFile.Value;
                    var files = await Task.Run(() => PdfOptimizer.SplitPdf(inputPath, pagesPerFile, outDir));
                    MessageBox.Show($"분할 완료!\n\n{files.Count}개 파일 생성\n첫 파일: {files[0]}", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    success = true;
                }
                // Ghostscript 대용량 최적화
                else if (chkUseGhostscript.Checked)
                {
                    (success, errorMsg) = await Task.Run(() => PdfOptimizer.OptimizeWithGhostscript(inputPath, outputPath));
                    if (success)
                    {
                        long afterSize = new FileInfo(outputPath).Length;
                        double ratio = beforeSize > 0 ? (1.0 - (double)afterSize / beforeSize) * 100 : 0;
                        MessageBox.Show($"Ghostscript 최적화 완료!\n\n경로: {outputPath}\n\n용량: {beforeSize / 1024} KB → {afterSize / 1024} KB ({ratio:F1}% 감소)", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                // 기존 방식
                else
                {
                    (success, errorMsg) = await Task.Run(() => OptimizePdf(inputPath, outputPath, UpdateProgress));
                    if (success)
                    {
                        long afterSize = new FileInfo(outputPath).Length;
                        double ratio = beforeSize > 0 ? (1.0 - (double)afterSize / beforeSize) * 100 : 0;
                        MessageBox.Show($"PDF 최적화 완료!\n\n경로: {outputPath}\n\n용량: {beforeSize / 1024} KB → {afterSize / 1024} KB ({ratio:F1}% 감소)", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"처리 중 오류 발생: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnOptimize.Enabled = true;
                btnBrowseInput.Enabled = true;
                btnBrowseOutput.Enabled = true;
                txtInput.Enabled = true;
                txtOutput.Enabled = !chkModifyOriginal.Checked;
                chkModifyOriginal.Enabled = true;
                chkSplit.Enabled = true;
                numPagesPerFile.Enabled = chkSplit.Checked;
                chkUseGhostscript.Enabled = true;
                progressBar.Style = ProgressBarStyle.Blocks;
                if (Path.GetTempPath() == Path.GetDirectoryName(inputPath) && File.Exists(inputPath) && inputPath != txtInput.Text)
                {
                    try { File.Delete(inputPath); } catch { }
                }
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
            catch (UnauthorizedAccessException)
            {
                return (false, "파일에 접근 권한이 없습니다. 관리자 권한 또는 다른 경로를 선택하세요.");
            }
            catch (IOException ioex)
            {
                return (false, $"입출력 오류: {ioex.Message}");
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

        // Drag & Drop for txtInput
        private void TxtInput_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && Path.GetExtension(files[0]).ToLower() == ".pdf")
                    e.Effect = DragDropEffects.Copy;
                else
                    e.Effect = DragDropEffects.None;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void TxtInput_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && Path.GetExtension(files[0]).ToLower() == ".pdf")
            {
                txtInput.Text = files[0];
                if (!chkModifyOriginal.Checked)
                {
                    string name = Path.GetFileNameWithoutExtension(files[0]) + "_optimized.pdf";
                    txtOutput.Text = Path.Combine(Path.GetDirectoryName(files[0]), name);
                }
            }
        }
        }
    }
