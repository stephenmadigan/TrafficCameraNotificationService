# TrafficCameraNotificationService

Demo gif: https://imgur.com/a/MJNTJ9k

C# .Net Program that simulates a red light traffic camera. Utilizes Amazon Rekognition (machine learning, image recognition), AWS S3, AWS SNS.

Image is uploaded via S3, processed using Amazons image recognition tools to obtain the text license plate number, then a notification is pushed to the users phone
letting them know their car was caught running a red light.





Future changes : 

  RDS/DynamoDB to store info about an individuals traffic infractions plus a reference to the image

  Export data from S3 to create a document incident log for each camera shot that includes the image, time, location etc used for court/ticket contesting.
 
Alternative : 

  Lambda program that triggers on S3 upload. Realistically all a traffic camera would do is snap an image then push it to S3, using AWS Lambda would be
  slightly more realistic though current program is fine for demo purposes.
