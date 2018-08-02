using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OMFFlights.FlightAwareXML2;
using Newtonsoft.Json;


namespace OMFFlights
{
    class FlightData
    {
        private static Boolean TURN_OFF_CERTIFICATE_VALIDATION = true;

        String airportCode = "AIRPORT_IDENTIFIER";

        private Dictionary<string, InFlightAircraftStruct> trackedFlights = new Dictionary<string, InFlightAircraftStruct>();

        private List<OMFEndpoint> endpoints = new List<OMFEndpoint>();

        private JsonSerializerSettings noNull = new JsonSerializerSettings {
                                 NullValueHandling = NullValueHandling.Ignore
    };

    public void init()
        {
            if (TURN_OFF_CERTIFICATE_VALIDATION)
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
            }

            endpoints.Add(new OMFEndpoint
            {
                url = "https://example.com",
                producerToken = "YOUR_PRODUCER_TOKEN_HERE"
            });

            createTypes(endpoints);


            // set up the source
            FlightXML2SoapClient client = new FlightXML2SoapClient();
            client.ClientCredentials.UserName.UserName = "username";
            client.ClientCredentials.UserName.Password = "FLIGHT_AWARE_API_KEY";

            AirportInfoStruct airport = client.AirportInfo(airportCode);

            sendAirport(endpoints, airportCode, airport);

            link(endpoints, new LinkRef { typeid = "airport", index = "_ROOT" }, new LinkRef { typeid = "airport", index = airportCode});

            EnrouteStruct r = client.Enroute(airportCode, 15, "", 0);

            foreach (EnrouteFlightStruct e in r.enroute)
            {

                if (isEnroute(e))
                {
                    trackedFlights.Add(e.ident, client.InFlightInfo(e.ident));

                    sendFlight(endpoints, e);

                    link(endpoints, new LinkRef { index = airportCode, typeid = "airport" }, new LinkRef { index = e.ident, typeid = "flight" });
                    link(endpoints, new LinkRef { index = e.ident, typeid = "flight" }, new LinkRef { containerid = e.ident });

                }

            }

            while (!Console.KeyAvailable)
            {

                foreach (KeyValuePair<string, InFlightAircraftStruct> entry in trackedFlights)
                {
                    InFlightData flightData = new InFlightData(entry.Value);

                    sendFlightData(endpoints, new Data<InFlightData>(entry.Key, true).Add(flightData));
                }

                Thread.Sleep(600000);
            }

        }


        private void createTypes(List<OMFEndpoint> endpoints)
        {
            TypeMessage airportType = new TypeMessage
            {
                id = "airport",
                classification = "static",
                name = "Airport",
            };
            airportType
                .AddProperty("Id", new OMFType { type = "string", isindex = true })
                .AddProperty("Name", new OMFType { type = "string", isname = true })
                .AddProperty("Latitude", new OMFType { type = "number", format = "float32" })
                .AddProperty("Longitude", new OMFType { type = "number", format = "float32" });


            foreach (OMFEndpoint endpoint in endpoints)
            {
                endpoint.sendMessage("create", "type", JsonConvert.SerializeObject(airportType, noNull));
            }

            TypeMessage flight = new TypeMessage
            {
                id = "flight",
                classification = "static",
                name = "Flight",
            };

            flight
                .AddProperty("Id", new OMFType { type = "string", isindex = true })
                .AddProperty("Name", new OMFType { type = "string", isname = true })
                .AddProperty("Destination", new OMFType { type = "string" })
                .AddProperty("Origin", new OMFType { type = "string" });

            foreach (OMFEndpoint endpoint in endpoints)
            {
                endpoint.sendMessage("create", "Type", JsonConvert.SerializeObject(flight, noNull));
            }

            TypeMessage flightUpdate = new TypeMessage
            {
                id = "flight_update",
                classification = "dynamic",
                name = "Flight Update",
            };

            flightUpdate
                .AddProperty("Time", new OMFType { type = "string", format = "date-time", isindex = true })
                .AddProperty("Groundspeed", new OMFType { type = "integer", format = "int32" })
                .AddProperty("Latitude", new OMFType { type = "number", format = "float32" })
                .AddProperty("Longitude", new OMFType { type = "number", format = "float32" });


            foreach (OMFEndpoint endpoint in endpoints)
            {
                endpoint.sendMessage("create", "type", JsonConvert.SerializeObject(flightUpdate, noNull));
            }

        }
        private void sendFlight(List<OMFEndpoint> endpoints, EnrouteFlightStruct flight)
        {
            foreach (OMFEndpoint endpoint in endpoints)
            {
                endpoint.sendMessage("create", "data", JsonConvert.SerializeObject(new Data<FlightInfo>("flight", false).Add(new FlightInfo(flight)), noNull));
                endpoint.sendMessage("create", "container", JsonConvert.SerializeObject(new Container(flight.ident, flight), noNull));
            }

        }

        private void sendFlightData<T>(List<OMFEndpoint> endpoints, Data<T> data)
        {
            foreach (OMFEndpoint endpoint in endpoints)
            {
                endpoint.sendMessage("create", "data", JsonConvert.SerializeObject(data, noNull));
            }
        }

        private void sendAirport(List<OMFEndpoint> endpoints, String airportCode, AirportInfoStruct airport)
        {
            foreach (OMFEndpoint endpoint in endpoints)
            {
                 endpoint.sendMessage("create", "data", 
                     JsonConvert.SerializeObject(new Data<AirportInfo>("airport", false).Add(new AirportInfo(airportCode, airport)), noNull));
            }
        }

        private void link(List<OMFEndpoint> endpoints, LinkRef airport, LinkRef flight)
        {
            LinkMessage linkMessage = new LinkMessage(airport, flight);

            foreach (OMFEndpoint endpoint in endpoints)
            {
                endpoint.sendMessage("create", "data", JsonConvert.SerializeObject(linkMessage, noNull));
            }
        }



        private bool isEnroute(EnrouteFlightStruct aircraft)
        {
            var utcNow = (TimeZoneInfo.ConvertTimeToUtc(DateTime.Now) -
                            new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            return aircraft.estimatedarrivaltime > utcNow && aircraft.actualdeparturetime > 0;
        }
    
    }

    class Container
    {
        public string id;
        public string typeid;
        // public string typeversion;
        public string name;
        public string description;
        public Array tags;
        public Dictionary<string, string> metadata;
        public Container(String id, EnrouteFlightStruct flight)
        {
            this.id = id;
            typeid = "flight_update";
            name = flight.originCity + "-" + flight.destinationCity;

        }

    }

    class TypeMessage
    {
        public string id;
        public string classification;
        public string version;
        public string name;
        public string description;
        public string tags;
        public Array metadata;
        public Dictionary<string, OMFType> properties = new Dictionary<string, OMFType>();
        public string type = "object";

        public TypeMessage AddProperty(String name, OMFType type)
        {
            properties.Add(name, type);
            return this;
        }
    }

    class OMFType
    {
        public string type;
        public bool isname;
        public bool isindex;
        public string format;
    }

    class Data<T>
    {
        public string typeid;
        public string containerid;
        public string typeversion;
        public List<T> values = new List<T>();

        public Data(String id, bool isDynamic)
        {
            if (isDynamic)
            {
                containerid = id;
            }
            else {
                typeid = id;
            }
        }

        public Data<T> Add(T value)
        {
            values.Add(value);
            return this;
        }

    }

    class InFlightData
    {
        public string Time;
        public int Groundspeed;
        public float Latitude;
        public float Longitude;

        public InFlightData(InFlightAircraftStruct flightUpdate)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(flightUpdate.timestamp).ToLocalTime();
            Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            Groundspeed = flightUpdate.groundspeed;
            Latitude = flightUpdate.latitude;
            Longitude = flightUpdate.longitude;
        }
    }

    class AirportInfo
    {
        public string Id;
        public string Name;
        public float Latitude;
        public float Longitude;

        public AirportInfo(String airportCode, AirportInfoStruct airport)
        {
            Id = airportCode;
            Latitude = airport.latitude;
            Longitude = airport.longitude;
            Name = airport.name;
        }
    }

    class FlightInfo
    {
        public string Id;
        public string Name;
        public string Origin;
        public string Destination;

        public FlightInfo(EnrouteFlightStruct flight)
        {
            Id = flight.ident;
            Name = flight.originCity + "-" + flight.destinationCity;
            Origin = flight.originCity;
            Destination = flight.destination;
        }
    }

    class LinkMessage
    {
        public string typeid = "__Link";
        public List<Link> values = new List<Link>();

        public LinkMessage(LinkRef source, LinkRef target)
        {
            values.Add(new Link(source, target));
        }
    }

    class Link
    {
        public LinkRef source;
        public LinkRef target;

        public Link(LinkRef sourceRef, LinkRef targetRef)
        {
            source = sourceRef;
            target = targetRef;
        }

        public Link(LinkRef sourceRef, String containerid)
        {
            source = sourceRef;
            target = new LinkRef { containerid = containerid };
        }

    }

    class LinkRef {
        public string typeid;
        public string index;
        public string containerid;
    }

    class OMFEndpoint {
        public string url;
        public string producerToken;

        public void sendMessage(String action, String message_type, String message_json)
        {
            message_json = "[" + message_json + "]";
            try
            {
                // Parse the message JSON into bytes
                byte[] message_json_bytes = System.Text.Encoding.ASCII.GetBytes(message_json);

                // Create a connection object
                System.Net.HttpWebRequest myRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                myRequest.Method = "POST";

                // The default is that no callback function is set and the ServerCertificateValidationCallback property is null.
                // Thus, all certificates will be accepted

                // Assemble headers that contain the producer token and message type
                // Note: in this example, the only action that is used is \"create\",
                // which will work totally fine;
                // to expand this application, you could modify it to use the \"update\"
                // action to, for example, modify existing AF element template types
                myRequest.Headers.Add("producertoken", producerToken);
                myRequest.Headers.Add("messagetype", message_type);
                myRequest.Headers.Add("action", action);
                myRequest.Headers.Add("messageformat", "JSON");
                myRequest.Headers.Add("omfversion", "1.0");

                // !!! Note: if desired, uncomment the below line to Console.WriteLine the outgoing message
                Console.WriteLine("\nOutgoing message (" + message_type + ", " + url + ")\n"  + message_json);
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
