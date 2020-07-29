using System;
using System.IO;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.S3.Transfer;
using System.Xml;
using System.Threading;

namespace TrafficCameraNotificationService
{
    class Program
    {
        static void Main()
        {
            SharedCredentialsFile sharedCredentialsFile = new SharedCredentialsFile();
            CredentialProfile defaultProfile = GetDefaultProfile(sharedCredentialsFile);
            if (defaultProfile == null)
            {
                Console.WriteLine("AWS [default] profile not found");
            }
            AWSCredentials credentials = AWSCredentialsFactory.GetAWSCredentials(defaultProfile, new SharedCredentialsFile());
            AmazonRekognitionClient client = new AmazonRekognitionClient(credentials, RegionEndpoint.USEast1);


            String[] testFiles = new string[] { "C:\\Users\\steph\\data\\License_Plate_Images\\plate1.jpg" , "C:\\Users\\steph\\data\\License_Plate_Images\\plate2.jpg",
            "C:\\Users\\steph\\data\\License_Plate_Images\\plate3.jpg","C:\\Users\\steph\\data\\License_Plate_Images\\plate4.jpg",
            "C:\\Users\\steph\\data\\License_Plate_Images\\plate5.jpg",  "C:\\Users\\steph\\data\\License_Plate_Images\\plate6.jpg" };


            Amazon.Rekognition.Model.Image i1 = new Amazon.Rekognition.Model.Image();
            Boolean run = true;
            Console.WriteLine("Traffic Camera Simulation");
            Console.WriteLine("6 test images found\n");

            while (run == true)
            {
                string image = "";
                Console.WriteLine("List of Recent images : \n");
                for (int i = 0; i < testFiles.Length; i++)
                {
                    Console.WriteLine("\t" + testFiles[i]);
                }
                Console.WriteLine("Press 1-6 to simulate traffic incident with specific vehicle, \"quit\" to quit: ");
                string imageSelect = Console.ReadLine();

                if (imageSelect == "1")
                    image = testFiles[0];
                else if (imageSelect == "2")
                    image = testFiles[1];
                else if (imageSelect == "3")
                    image = testFiles[2];
                else if (imageSelect == "4")
                    image = testFiles[3];
                else if (imageSelect == "5")
                    image = testFiles[4];
                else if (imageSelect == "6")
                    image = testFiles[5];
                else if (imageSelect == "quit")
                    run = false;
                try
                {
                    using FileStream fs = new FileStream(image, FileMode.Open, FileAccess.Read);
                    byte[] data = null;
                    data = new byte[fs.Length];
                    fs.Read(data, 0, (int)fs.Length);
                    i1.Bytes = new MemoryStream(data);
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to load file" + image);
                }
                GetTextFromImage(client, i1, image, credentials);
                Thread.Sleep(10000);
            }
            Console.ReadLine();
        }

        public static async void GetTextFromImage(AmazonRekognitionClient client, Amazon.Rekognition.Model.Image image, string file, AWSCredentials credentials)
        {
            var textRequest = new DetectTextRequest
            {
                Image = image
            };
            string plateDetails = "";
            try
            {
                DetectTextResponse textResponse = await client.DetectTextAsync(textRequest);
                foreach (TextDetection t in textResponse.TextDetections)
                {
                    string possiblePlate = t.DetectedText;
                    if (IsPlateNumber(possiblePlate) == true)
                    {
                        plateDetails = possiblePlate;
                    }
                }
                Console.WriteLine("\n\tplate found : " + plateDetails + "\n");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            if (plateDetails != "")
            {
                PushPlateToS3(file);
                NotifyUser(plateDetails, credentials);
            }
        }

        public static bool IsPlateNumber(string possiblePlate)
        {
            if (IsCapitalLettersAndNumbers(possiblePlate) == true)
            {
                if (!possiblePlate.Contains(" "))
                {
                    string testString = possiblePlate.ToUpper();
                    if (testString.CompareTo(possiblePlate) == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool IsCapitalLettersAndNumbers(string s)
        {
            System.Text.RegularExpressions.Regex rg1 = new System.Text.RegularExpressions.Regex("[^A-Z]");
            System.Text.RegularExpressions.Regex rg2 = new System.Text.RegularExpressions.Regex("[^0-9]");
            if (rg1.IsMatch(s) && rg2.IsMatch(s))
            {
                return true;
            }
            return false;
        }

        private static async void PushPlateToS3(string file)
        {

            string bucketName = "XXXXXX-XXXXX-XXXX";
            string filePath = file;
            RegionEndpoint bucketRegion = RegionEndpoint.USEast1;

            IAmazonS3 s3Client = new AmazonS3Client(bucketRegion);
            try
            {
                var fileTransferUtility = new TransferUtility(s3Client);
                await fileTransferUtility.UploadAsync(filePath, bucketName);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static async void NotifyUser(string plateDetails, AWSCredentials credentials)
        {
            AmazonSimpleNotificationServiceClient AmazonSNSClient = new AmazonSimpleNotificationServiceClient(credentials, RegionEndpoint.USEast1);
            string AWSArn = "arn:aws:sns:us-east-1:XXXXXXXXXXXXX:PhoneLookupTopic";

            XmlNode userData = LookupPlateInfo(plateDetails);
            if (userData == null)
            {
                Console.WriteLine("Vehicle not found in database");
            }
            else
            {
                XmlNode colorNode = userData.SelectSingleNode("color");
                XmlNode makeNode = userData.SelectSingleNode("make");
                XmlNode modelNode = userData.SelectSingleNode("model");
                XmlNode ownerNode = userData.SelectSingleNode("owner");
                XmlNode phoneNode = ownerNode.SelectSingleNode("phone");

                string phoneNumber = phoneNode.InnerText;
                string color = colorNode.InnerText;
                string make = makeNode.InnerText;
                string model = modelNode.InnerText;
                string message = "Your " + color + " " + make + " " + model + "(license plate [" + plateDetails + "])" +
                    " was involved in a traffic violation.\n\t A ticket was mailed to your address.";
                SubscribeRequest subscribeRequest = new SubscribeRequest(AWSArn, "sms", phoneNumber);
                SubscribeResponse subscribeResponse = await AmazonSNSClient.SubscribeAsync(subscribeRequest);

                PublishRequest request = new PublishRequest
                {
                    Message = message,
                    PhoneNumber = phoneNumber
                };

                PublishResponse publishResponse = await AmazonSNSClient.PublishAsync(request);
                Console.WriteLine("\n\tuser notified of traffic incident, messageID : " + publishResponse.ResponseMetadata.RequestId);
                Console.WriteLine("\n\tmessage sent to phone : \n\n\t" + message + "\n");
            }


        }

        private static XmlNode LookupPlateInfo(string plateDetails)
        {
            string dmv = @"<database>
            <vehicle>
                <plate>6TRJ244</plate>
                <make>Ford</make>
                <model>Focus</model>
                <color>Red</color>
                <owner>
                    <name>John Smith</name>
                    <phone>+1XXXXXXXXXX</phone>
                </owner>
            </vehicle>
            <vehicle>
                <plate>5ALN015</plate>
                <make>Honda</make>
                <model>Civic</model>
                <color>Blue</color>
                <owner>
                    <name>Jennifer Hartley</name>
                    <phone>+1XXXXXXXXXX</phone>
                </owner>
            </vehicle>
            <vehicle>
                <plate>7TRR812</plate>
                <make>Jeep</make>
                <model>Wrangler</model>
                <color>Yellow</color>
                <owner>
                    <name>Matt Johnson</name>
                    <phone>+1XXXXXXXXXX</phone>
                </owner>
            </vehicle>
            <vehicle>
                <plate>3ZZB646</plate>
                <make>Honda</make>
                <model>CRV</model>
                <color>Silver</color>
                <owner>
                    <name>Dawn Fink</name>
                    <phone>+1XXXXXXXXXX</phone>
                </owner>
            </vehicle>
            <vehicle>
                <plate>6YMX832</plate>
                <make>Chevrolet</make>
                <model>Cruze</model>
                <color>Red</color>
                <owner>
                    <name>Tim Carpenter</name>
                    <phone>+1XXXXXXXXXX</phone>
                </owner>
            </vehicle>
        </database>";
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(dmv);
            XmlElement rootElement = xmlDoc.DocumentElement;
            XmlNode n2 = rootElement.SelectSingleNode("vehicle[plate=\"" + plateDetails + "\"]");
            if (n2 != null)
            {
                return n2;
            }
            return null;
        }

        private static CredentialProfile GetDefaultProfile(SharedCredentialsFile sharedCredentialsFile)
        {
            if (sharedCredentialsFile == null)
            {
                throw new ArgumentNullException("argument sharedCredentialsFile is null");
            }
            const string DEFAULT_PROFILE = "default";
            return sharedCredentialsFile.ListProfiles().Find(p => p.Name.Equals(DEFAULT_PROFILE));
        }
    }
}