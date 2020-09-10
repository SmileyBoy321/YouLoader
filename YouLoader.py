from win10toast import ToastNotifier
from tkinter import filedialog
import tkinter as tk
import youtube_dl
import pyperclip
import os
import sys

root = tk.Tk()
root.withdraw()
toast = ToastNotifier()

ydl_opts = {
    "format": "bestaudio/best",
    "noplaylist": True,
    "source_address": "0.0.0.0",
    "postprocessors": [{"key": "FFmpegExtractAudio", "preferredcodec": "mp3"}],
}

copied_link = pyperclip.paste()
iconPath = os.path.abspath(os.path.join(os.path.dirname(__file__), "icon.ico"))

if "youtube.com" in copied_link:
	try:
	    file_path = filedialog.askdirectory()
	    os.chdir(file_path)
	    with youtube_dl.YoutubeDL(ydl_opts) as ydl:
	        ydl.download([f"{copied_link}"])
	        toast.show_toast(
	            "Music downloaded", "Ready to download another one", duration=2, icon_path=iconPath
	        )
	except OSError:
		sys.exit(0) # die quietly snake and don't bother the user
		# error happens when user doesn't choose a directory and clicks X
		# button during the fileDirectory input
else:
    toast.show_toast(
        "Invalid youtube link",
        "Could not download the music",
        duration=2,
        icon_path=iconPath,
    )
