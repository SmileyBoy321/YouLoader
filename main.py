# This Python file uses the following encoding: utf-8
import youtube_dl
import sys
import os


from PySide2.QtWidgets import (QApplication, QWidget,
                               QPushButton, QLineEdit,
                               QFileDialog, QLabel,
                               QProgressBar, QRadioButton
                               )
from PySide2.QtCore import QFile, QThread, Signal
from PySide2.QtUiTools import QUiLoader


class GUI(QWidget):
    def __init__(self):
        super(GUI, self).__init__()
        self.load_ui()

        self.dirPath = self.findChild(QLineEdit, "lineEditDir")
        self.downloadURL = self.findChild(QLineEdit, "lineEditURL")
        self.browse = self.findChild(QPushButton, "pushButtonDir")
        self.download = self.findChild(QPushButton, "pushButtonDownload")
        self.progressText = self.findChild(QLabel, "lblProgressText")
        self.progressBar = self.findChild(QProgressBar, "progressBar")
        self.mp3btn = self.findChild(QRadioButton, "mp3")

        self.worker = Worker(self)
        self.browse.clicked.connect(self.onBrowseClicked)
        self.worker.updateProgress.connect(self.setProgress)
        self.worker.updateText.connect(self.setText)
        self.download.clicked.connect(self.worker.start)

    def load_ui(self):
        loader = QUiLoader()
        path = os.path.join(os.path.dirname(__file__), "form.ui")
        ui_file = QFile(path)
        ui_file.open(QFile.ReadOnly)
        loader.load(ui_file, self)
        ui_file.close()

    def onBrowseClicked(self):
        filename = QFileDialog.getExistingDirectory()

        if filename:
            self.dirPath.setText(filename)
            os.chdir(self.dirPath.text())

    def setProgress(self, progress):
        self.progressBar.setValue(progress)

    def setText(self, str):
        self.progressText.setText(str)


class Worker(QThread):
    updateProgress = Signal(float)
    updateText = Signal(str)

    def __init__(self, main_window):
        self.main_window = main_window
        super(Worker, self).__init__(main_window)

    def run(self):
        self.main_window.progressBar.setValue(0)

        self.ydl_opts = {
            'format': 'bestaudio/best',
            'noplaylist': True,
            'source_address': '0.0.0.0',
            'progress_hooks': [self.my_hook],
            'postprocessors': [{
                'key': 'FFmpegExtractAudio',
                'preferredcodec': 'mp3'
            }]
        }

        with youtube_dl.YoutubeDL(self.ydl_opts) as ydl:
            ydl.download([f'{self.main_window.downloadURL.text()}'])

    def my_hook(self, download):
        if download["status"] == "finished":
            print("Finished")
            self.main_window.progressText.setText("Ready")
        if download["status"] == "downloading":
            self.updateText.emit("Downloading...")
            percent = download["_percent_str"]
            percent = percent.replace("%", "")
            self.updateProgress.emit(float(percent))


if __name__ == "__main__":
    app = QApplication([])
    widget = GUI()
    widget.show()
    sys.exit(app.exec_())
