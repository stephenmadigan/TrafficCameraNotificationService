# TrafficCameraNotificationService

Demo gif: https://imgur.com/a/MJNTJ9k

C# .Net Program that simulates a red light traffic camera. Utilizes Amazon Rekognition (machine learning, image recognition), AWS S3, AWS SNS.

Image is uploaded via S3, processed using Amazons image recognition tools to obtain the text license plate number, then a notification is pushed to the users phone
letting them know their car was caught running a red light.
