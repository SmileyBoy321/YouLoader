FROM python:3.8.6-alpine

WORKDIR /app

ADD src/ /app

RUN apk --no-cache add ffmpeg 

RUN pip --no-cache-dir install -r /app/requirements.txt

EXPOSE 5000

CMD ["python", "YouLoader.py"]
