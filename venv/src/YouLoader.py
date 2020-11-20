from flask import Flask, url_for, render_template, request
import os
import youtube_dl

app = Flask(__name__)
app.secret_key = b"\xfc\x06Ah\xad\xfc\x8d.O\xc7:\xa6\x02\x90\n\x13"
os.chdir(os.path.join(os.path.expanduser("~"), "Downloads")) # user's Downloads dir

ydl_opts = {
    "format": "bestaudio/best",
    "noplaylist": True,
    "source_address": "0.0.0.0",
    "postprocessors": [{"key": "FFmpegExtractAudio", "preferredcodec": "mp3"}],
}

@app.route("/")
def index():
	url_for("static", filename="style.css")
	return render_template("index.html")

@app.route("/", methods=["POST"])
def download_button():
	text = request.form["url_link"]
	if request.method == "POST":
		if "youtube.com" in text:
			print("Current dir:", os.getcwd())
			print("Has youtube.com url in it")
			with youtube_dl.YoutubeDL(ydl_opts) as ydl:
				ydl.download([f"{text}"])

	return render_template("index.html")

if __name__ == "__main__":
	app.run(debug=True)
