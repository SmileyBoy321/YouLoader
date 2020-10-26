from win10toast import ToastNotifier
import pyperclip
import os
import sys
import subprocess

toast = ToastNotifier()

copied_link = pyperclip.paste()
iconPath = os.path.abspath(os.path.join(os.path.dirname(__file__), "icon.ico"))
desktop = os.path.join(os.path.join(os.environ["USERPROFILE"]), "Desktop")
os.chdir(desktop)

if "youtube.com" in copied_link:
    try:
        if os.path.isdir("Music"):
            os.chdir("Music")
            subprocess.Popen(
                ["youtube-dl", "--no-playlist", "-x", "--audio-format", "mp3", copied_link], shell=True
            )
            toast.show_toast(
                "Music downloaded",
                "Ready to download another one",
                duration=2,
                icon_path=iconPath,
            )
        else:
            os.mkdir("Music")
            os.chdir("Music")
            subprocess.Popen(
                ["youtube-dl", "--no-playlist", "-x", "--audio-format", "mp3", copied_link], shell=True
            )
            toast.show_toast(
                "Music downloaded",
                "Ready to download another one",
                duration=2,
                icon_path=iconPath,
            )
    except OSError:
        sys.exit(0)  # Let python program die quietly and not bother the user
        # error happens when user doesn't choose a directory and clicks X
        # button during the fileDirectory input
else:
    toast.show_toast(
        "Invalid youtube link",
        "Could not download the music",
        duration=2,
        icon_path=iconPath,
    )
