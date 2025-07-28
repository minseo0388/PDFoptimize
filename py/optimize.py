import os
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
import fitz  # PyMuPDF
import threading

def optimize_pdf(input_path, output_path, progress_callback=None):
    """
    Optimize a PDF file and save to output_path.
    progress_callback: function(percent:int) to update progress.
    """
    try:
        doc = fitz.open(input_path)
        total_pages = len(doc)
        # Dummy progress: just iterate pages to simulate work
        for i, _ in enumerate(doc):
            percent = int(((i + 1) / total_pages) * 100)
            if progress_callback:
                progress_callback(percent)
        # Save with optimization options
        doc.save(output_path, garbage=4, deflate=True, clean=True)
        doc.close()
        if progress_callback:
            progress_callback(100)
        return True, None
    except Exception as e:
        return False, str(e)

def threaded_optimize(in_path, out_path, progress_bar, on_done):
    def update_progress(val):
        progress_bar["value"] = val
        root.update_idletasks()
    success, err = optimize_pdf(in_path, out_path, update_progress)
    if success:
        messagebox.showinfo("완료", f"PDF 최적화 완료:\n{out_path}")
    else:
        messagebox.showerror("오류", f"최적화 중 오류 발생:\n{err}")
    if on_done:
        on_done()

def select_file():
    path = filedialog.askopenfilename(filetypes=[("PDF files", "*.pdf")])
    if path:
        input_path.set(path)
        default_name = os.path.splitext(os.path.basename(path))[0] + "_optimized.pdf"
        output_filename.set(default_name)
        update_save_button_state()


def select_folder():
    folder = filedialog.askdirectory()
    if folder:
        output_folder.set(folder)
        update_save_button_state()


def update_save_button_state():
    if input_path.get() and (modify_original.get() or output_folder.get()):
        start_btn.config(state="normal")
    else:
        start_btn.config(state="disabled")


def start_optimization():
    in_path = input_path.get()
    if not os.path.isfile(in_path):
        messagebox.showerror("오류", "입력 파일이 존재하지 않습니다.")
        return
    if modify_original.get():
        out_path = in_path
    else:
        if not output_folder.get():
            messagebox.showwarning("경고", "저장할 폴더를 선택하세요.")
            return
        out_path = os.path.join(output_folder.get(), output_filename.get())
        if os.path.abspath(in_path) == os.path.abspath(out_path):
            messagebox.showerror("오류", "입력 파일과 출력 파일이 동일합니다.")
            return
    start_btn.config(state="disabled")
    progress["value"] = 0
    threading.Thread(target=threaded_optimize, args=(in_path, out_path, progress, lambda: start_btn.config(state="normal")), daemon=True).start()

# GUI Part Below This Line
root = tk.Tk()
root.title("PDF 최적화 도구")
root.geometry("500x300")

input_path = tk.StringVar()
output_folder = tk.StringVar()
output_filename = tk.StringVar()
modify_original = tk.BooleanVar()

# 파일 선택
tk.Label(root, text="PDF 파일 선택:").pack(pady=5)
tk.Entry(root, textvariable=input_path, width=60, state="readonly").pack()
tk.Button(root, text="파일 선택", command=select_file).pack(pady=5)

# 원본 수정 여부
tk.Checkbutton(root, text="원본 파일 수정", variable=modify_original,
               command=update_save_button_state).pack(pady=5)

# 저장 경로와 이름 (원본 수정 선택 안 했을 때)
tk.Label(root, text="저장할 폴더 및 이름 (선택적):").pack()
tk.Entry(root, textvariable=output_folder, width=60, state="readonly").pack()
tk.Button(root, text="폴더 선택", command=select_folder).pack(pady=5)
tk.Entry(root, textvariable=output_filename, width=40).pack()

# 진행 바
progress = ttk.Progressbar(root, orient="horizontal", length=400, mode="determinate")
progress.pack(pady=15)

# 실행 버튼
start_btn = tk.Button(root, text="최적화 실행", state="disabled", command=start_optimization)
start_btn.pack()

root.mainloop()