from win10toast import ToastNotifier
from tkinter import filedialog
import tkinter as tk
import youtube_dl
import pyperclip
import os
root = tk.Tk()
root.withdraw()
toaster = ToastNotifier()


ydl_opts = {
    'format': 'bestaudio/best',
    'noplaylist': True,
    'source_address': '0.0.0.0',
    'postprocessors': [{
        'key': 'FFmpegExtractAudio',
        'preferredcodec': 'mp3'
    }]
}

copied_link = pyperclip.paste()
if "youtube.com" in copied_link:
    file_path = filedialog.askdirectory()
    os.chdir(file_path)
    with youtube_dl.YoutubeDL(ydl_opts) as ydl:
        ydl.download([f'{copied_link}'])
        toaster.show_toast("Finished downloading music",
                           "Now converting the music to mp3",
                           icon_path=r"C:\Users\SmileyBoy\Desktop\icon.ico")
else:
    toaster.show_toast("Invalid link",
                       "Did not find youtube.com in the link",
                       icon_path=r"C:\Users\SmileyBoy\Desktop\icon.ico")

