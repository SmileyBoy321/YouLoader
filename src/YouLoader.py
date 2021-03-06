from flask import Flask, url_for, render_template, request, send_from_directory, flash, redirect
import os
import youtube_dl
import logging

# Download music to this path
MUSIC_FOLDER = "static/music/"
if not os.path.exists(MUSIC_FOLDER):
    os.makedirs(MUSIC_FOLDER)

app = Flask(__name__)
app.secret_key = b"\xfc\x06Ah\xad\xfc\x8d.O\xc7:\xa6\x02\x90\n\x13"

file_name = None
logging.basicConfig(level=logging.DEBUG)


def my_hook(d):
    global file_name
    if d["status"] == "finished":
        file_name = d["filename"].split("/")[-1].split(".")
        file_name[1] = "mp3"
        file_name = ".".join(file_name)

outtmpl = MUSIC_FOLDER + "%(title)s.%(ext)s"
ydl_opts = {
    "format": "bestaudio/best",
    "outtmpl": outtmpl,
    "noplaylist": True,
    "postprocessors": [{"key": "FFmpegExtractAudio", "preferredcodec": "mp3"}],
    "progress_hooks": [my_hook],
}

# Main application interface
@app.route("/")
def index():
    url_for("static", filename="style.css")
    return render_template("index.html", title="YouLoader")


# Download route to add a URL to be downloaded
@app.route("/download", methods=["POST"])
def download():
    global file_name
    text = request.form["url_link"]
    if request.method == "POST":
        if "youtube.com" in text or "soundcloud.com" in text:
            try:
                with youtube_dl.YoutubeDL(ydl_opts) as ydl:
                    ydl.download([f"{text}"])
                    for filename in os.listdir(MUSIC_FOLDER):
                        if filename == file_name:
                            file_name = filename
                        else:
                            os.remove(MUSIC_FOLDER + filename)
                    return send_from_directory(
                        MUSIC_FOLDER,
                        file_name,
                        mimetype="audio/mpeg",
                        attachment_filename=file_name,
                        as_attachment=True,
                    )
            except Exception as e:
                logging.debug(e)
                return redirect(url_for("index"))
    return redirect(url_for("index"))


if __name__ == "__main__":
    app.run(host='0.0.0.0', debug=False)
