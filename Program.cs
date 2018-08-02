using System;

namespace OMFFlights
{
    public class Program
    {
        // ************************************************************************
        // Specify constant values (names, target URLS, etc.) needed by the script
        // ************************************************************************

        // !!! Note: if you'd like to test this code against an endpoint with
        // an untrusted certificate, set the following flag to true to turn off
        // certificate validation.  THIS IS INSECURE AND SHOULD NOT BE USED IN
        // PRODUCTION DEPLOYMENTS!
        private static Boolean TURN_OFF_CERTIFICATE_VALIDATION = true;

        // Specify the name of this device, or simply use the hostname; this is the name
        // of the PI AF Element that will be created, and it'll be included in the names
        // of PI Points that get created as well
        private static String DEVICE_NAME = System.Net.Dns.GetHostName() + "OMFFlights";
        //DEVICE_NAME = "MyCustomDeviceName"

        // Specify a device location (optional); this will be added as a static
        // string attribute to the AF Element that is created
        private static String DEVICE_LOCATION = "OSI Flight Tracking Center";

        // Specify the name of the Assets type message; this will also end up becoming
        // part of the name of the PI AF Element template that is created; for example, this could be
        // "AssetsType_RaspberryPI" or "AssetsType_Dragonboard"
        // You will want to make this different for each general class of IoT module that you use
        private static String ASSETS_MESSAGE_TYPE_NAME = DEVICE_NAME + "_assets_type";
        //ASSETS_MESSAGE_TYPE_NAME = "assets_type" + "IoT Device Model 74656" // An example

        // Similarly, specify the name of for the data values type; this should likewise be unique
        // for each general class of IoT device--for example, if you were running this
        // script on two different devices, each with different numbers and kinds of sensors,
        // you'd specify a different data values message type name
        // when running the script on each device.  If both devices were the same,
        // you could use the same DATA_VALUES_MESSAGE_TYPE_NAME
        private static String DATA_VALUES_MESSAGE_TYPE_NAME = "data_values_type" + "";
        //DATA_VALUES_MESSAGE_TYPE_NAME = "data_values_type" + "IoT Device Model 74656" // An example

        // Store the id of the container that will be used to receive live data values
        private static String DATA_VALUES_CONTAINER_ID = DEVICE_NAME + "_data_values_container";

        // Specify the number of seconds to sleep in between value messages
        private static int NUMBER_OF_SECONDS_BETWEEN_VALUE_MESSAGES = 2;

        // Specify whether you're sending data to OSIsoft cloud services or not
        private static Boolean SEND_DATA_TO_OSISOFT_CLOUD_SERVICES = false;

        // Specify the address of the destination endpoint; it should be of the form
        // http://<host/ip>:<port>/ingress/messages
        // For example, "https://myservername:5460/ingress/messages"
        private static String TARGET_URL = "https://jharlan-pi.osisoft.int:8118/ingress/messages";
        // !!! Note: if sending data to OSIsoft cloud services,
        // uncomment the below line in order to set the target URL to the OCS OMF endpoint:
        //TARGET_URL = "https://dat-a.osisoft.com/api/omf"
        // !!! Note: if sending data to a local Edge Data Store,
        // uncomment the below line in order to set the target URL to the default EDS endpoint:
        //TARGET_URL = "https://localhost:5000/edge/omf/tenants/default/namespaces/data";

        // Specify the producer token, a unique token used to identify and authorize a given OMF producer. Consult the OSIsoft Cloud Services or PI Connector Relay documentation for further information.
        private static String PRODUCER_TOKEN = "uid=1d6643ba-70d4-413e-9367-3f373d32edfc&crt=20180801190029370&sig=OO1KbMFIa0pPvqtzM6xF1Yakzi6GwOjB4ft9lomTg+s=";
        // !!! Note: if sending data to OSIsoft cloud services, the producer token should be the
        // security token obtained for a particular Tenant and Publisher; see
        // http://qi-docs.readthedocs.io/en/latest/OMF_Ingress_Specification.html//headers
        // !!! Note: if sending data to the Edge Data Store, for information on the producer token
        // that should be used, see
        // http://osisoft-edge.readthedocs.io/en/latest/EdgeSecurity.html#api-security 

        // ************************************************************************
        // Helper function: run any code needed to initialize local sensors, if necessary for this hardware
        // ************************************************************************

        // Below is where you can initialize any global variables that are needed by your application;
        // certain sensors, for example, will require global interface or sensor variables
        // myExampleInterfaceKitGlobalVar = None

        // The following function is where you can insert specific initialization code to set up
        // sensors for a particular IoT module or platform
        public static void Main()
        {
            var fd = new FlightData();
            fd.init();
        }



        private static void send_omf_message_to_endpoint(String action, String message_type, String message_json)
        {
            try
            {
                // Parse the message JSON into bytes
                byte[] message_json_bytes = System.Text.Encoding.ASCII.GetBytes(message_json);

                // Create a connection object
                System.Net.HttpWebRequest myRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(TARGET_URL);
                myRequest.Method = "POST";

                // The default is that no callback function is set and the ServerCertificateValidationCallback property is null.
                // Thus, all certificates will be accepted

                // Assemble headers that contain the producer token and message type
                // Note: in this example, the only action that is used is \"create\",
                // which will work totally fine;
                // to expand this application, you could modify it to use the \"update\"
                // action to, for example, modify existing AF element template types
                myRequest.Headers.Add("producertoken", PRODUCER_TOKEN);
                myRequest.Headers.Add("messagetype", message_type);
                myRequest.Headers.Add("action", action);
                myRequest.Headers.Add("messageformat", "JSON");
                myRequest.Headers.Add("omfversion", "1.0");

                // !!! Note: if desired, uncomment the below line to Console.WriteLine the outgoing message
                Console.WriteLine("\nOutgoing message:\n" + message_json);
                // Send the request, and collect the response
                System.IO.Stream requestStream = myRequest.GetRequestStream();
                requestStream.Write(message_json_bytes, 0, message_json_bytes.Length);
                requestStream.Close();

                // Show the response
                System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)myRequest.GetResponse();
                Console.WriteLine("Response code: " + (int)response.StatusCode);
                // For more about response codes, see
                // https://omf-docs.readthedocs.io/en/v1.0/Standard_Responses.html
            }
            catch (System.Net.WebException ex)
            {
                if (ex.Response != null)
                {
                    // Catch a web-specific error; get the response stream, and read throuh it
                    using (var responseStream = ex.Response.GetResponseStream())
                    using (var reader = new System.IO.StreamReader(responseStream))
                    {
                        // Print out the specific web error to the console
                        Console.WriteLine("!!!!! " + DateTime.Now + " Error during web request: " + reader.ReadToEnd() + " (" + ex.Message + ")");
                    }
                }
                else
                {
                    Console.WriteLine("!!!!! " + DateTime.Now + " General error during web request: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                // Log any GENERAL error, if it occurs
                Console.WriteLine("!!!!! " + DateTime.Now + " General error during web request: " + ex.Message);
            }
        }
    }


}
