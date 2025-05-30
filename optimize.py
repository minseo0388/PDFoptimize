import os
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
import fitz  # PyMuPDF
import threading

def optimize_pdf(input_path, output_path, progress_bar):
    try:
        doc = fitz.open(input_path)
        total_pages = len(doc)
        
        for i, _ in enumerate(doc):
            progress = int(((i + 1) / total_pages) * 100)
            progress_bar["value"] = progress
            root.update_idletasks()

        doc.save(output_path, garbage=4, deflate=True, clean=True)
        doc.close()

        progress_bar["value"] = 100
        messagebox.showinfo("완료", f"PDF 최적화 완료:\n{output_path}")
    except Exception as e:
        messagebox.showerror("오류", f"최적화 중 오류 발생:\n{str(e)}")


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
    
    if modify_original.get():
        out_path = in_path
    else:
        if not output_folder.get():
            messagebox.showwarning("경고", "저장할 폴더를 선택하세요.")
            return
        out_path = os.path.join(output_folder.get(), output_filename.get())
    
    # 실행 쓰레드 분리
    threading.Thread(target=optimize_pdf, args=(in_path, out_path, progress)).start()


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