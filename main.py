# This Python file uses the following encoding: utf-8
import sys
import os
from PySide2.QtWidgets import QApplication, QSystemTrayIcon, QFileDialog
from PySide2.QtGui import QIcon
from PySide2.QtCore import QCoreApplication
import youtube_dl
import pyperclip
import Resources


class SystemTrayIcon:
    def __init__(self):
        self.icon = QIcon(":/icons/icon.ico")
        self.tray = QSystemTrayIcon(self.icon)
        self.tray.show()
        self.tray.activated.connect(self.right_or_left_click)

        self.ydl_opts = {
            "format": "bestaudio/best",
            "noplaylist": True,
            "source_address": "0.0.0.0",
            "postprocessors": [{"key": "FFmpegExtractAudio", "preferredcodec": "mp3"}],
        }

    def right_or_left_click(self, reason):
        copied_link = pyperclip.paste()
        if reason == QSystemTrayIcon.ActivationReason.Trigger:
            if "youtube.com" in copied_link:
                filepath = QFileDialog.getExistingDirectory()
                os.chdir(filepath)
                with youtube_dl.YoutubeDL(self.ydl_opts) as ydl:
                    ydl.download([f"{copied_link}"])
                    self.tray.showMessage(
                        "Finished downloading the music",
                        "Now converting the music to mp3",
                        self.icon,
                        msec=1000,
                    )
            else:
                self.tray.showMessage(
                    "Invalid link",
                    "Did not find youtube.com in the link",
                    self.icon,
                    msec=1000,
                )
        elif reason == QSystemTrayIcon.ActivationReason.Context:
            QCoreApplication.exit()

    def exitClicked(self):
        QCoreApplication.exit()


if __name__ == "__main__":
    app = QApplication([])
    widget = SystemTrayIcon()
    sys.exit(app.exec_())
