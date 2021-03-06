# YouLoader

> Web browser program to download music from YouTube and SoundCloud as MP3 with the click of a button.

![youloader_pic](https://user-images.githubusercontent.com/45132310/100654190-2077f780-3352-11eb-8e4b-a6ca144b62e0.png)

## Getting started

These instructions will get you a copy of the project up and running on your local machine for development and testing purposes.
```
git clone https://github.com/SmileyBoy321/YouLoader.git
cd YouLoader/src/ && pip install -r requirements.txt && cd ..
flask run
```

### Prerequisites
Install via pip commands manually or via requirements.txt mentioned above
```
sudo pip install ffmpeg youtube-dl flask
```


## Built With
* Front and Back end: [flask](https://github.com/pallets/flask)
* Music downloader: [youtube-dl](https://github.com/ytdl-org/youtube-dl)
* Music converter: [FFmpeg](https://github.com/FFmpeg/FFmpeg)

## Docker deployment
Have to be in the directory of where Dockerfile exists.  
**Manjaro Openbox:**
```
sudo pacman -S docker  
docker build -t youloader-web:latest .  
docker run -it --rm --publish 5000:5000 youloader-web:latest
```
# Contributing
I wanted a simple and effective website to download youtube music and so I made this. If you have any ideas for this project, just open an issue and tell me what you think.
If you'd like to contribute, please fork the repository and make changes as you'd like. Pull requests are warmly welcome.

# Licensing
This project is licensed under [MIT license](https://github.com/SmileyBoy321/YouLoader/blob/master/LICENSE).
