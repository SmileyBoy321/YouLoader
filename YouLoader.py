from tkinter import filedialog
import tkinter as tk
import youtube_dl
import pyperclip
import os

root = tk.Tk()
root.withdraw()


ydl_opts = {
    "format": "bestaudio/best",
    "noplaylist": True,
    "source_address": "0.0.0.0",
    "postprocessors": [{"key": "FFmpegExtractAudio", "preferredcodec": "mp3"}],
}

copied_link = pyperclip.paste()

if "youtube.com" in copied_link:
    file_path = filedialog.askdirectory()
    os.chdir(file_path)
    with youtube_dl.YoutubeDL(ydl_opts) as ydl:
        ydl.download([f"{copied_link}"])
