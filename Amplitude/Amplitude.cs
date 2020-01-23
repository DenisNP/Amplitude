using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

namespace Amplitude
{
    public class Amplitude
    {
        private static string _apiKey;
        private const string ApiAddress = "https://api.amplitude.com/httpapi";
        private static readonly HttpClient Client = new HttpClient();
        private static readonly ConcurrentDictionary<string, Amplitude> Instances = new ConcurrentDictionary<string, Amplitude>();

        private readonly string _userId;
        private readonly Dictionary<string, object> _persistentProperties;
        private long _sessionStartTime = -1;
        
        /// <summary>
        /// Initialize entire service with api key
        /// </summary>
        /// <param name="apiKey">Your API_KEY from amplitude console</param>
        public static void Initialize(string apiKey)
        {
            _apiKey = apiKey;
        }

        /// <summary>
        /// Get or create instance for specific user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="persistentProperties">Properties that should be added to each event for this user</param>
        /// <returns>Amplitude service instance</returns>
        public static Amplitude InstanceFor(string userId, Dictionary<string, object> persistentProperties = null)
        {
            return Instances.GetOrAdd(userId, new Amplitude(userId, persistentProperties));
        }

        /// <summary>
        /// Dispose instance for specific user
        /// </summary>
        /// <param name="userId">User identifier</param>
        public static void DisposeFor(string userId)
        {
            Instances.TryRemove(userId, out _);
        }

        private Amplitude(string userId, Dictionary<string, object> persistentProperties = null)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new Exception("You should call Amplitude.Initialize(...) with your API_KEY first");
            }

            _userId = userId;
            _persistentProperties = persistentProperties;
        }

        /// <summary>
        /// Start new session to track
        /// </summary>
        /// <param name="startTime">Session start time, defaults to now</param>
        /// <returns>Amplitude service instance for chaining, e.g. `var amp = Amplitude.InstanceFor(...).StartSession()`</returns>
        public Amplitude StartSession(DateTimeOffset startTime = default)
        {
            _sessionStartTime = startTime == default
                ? DateTimeOffset.Now.ToUnixTimeMilliseconds()
                : startTime.ToUnixTimeMilliseconds();

            return this;
        }

        /// <summary>
        /// Track event
        /// </summary>
        /// <param name="eventName">String name of the event</param>
        /// <param name="properties">Additional event data, this will be added to persistent properties</param>
        public void Track(string eventName, Dictionary<string, object> properties = null)
        {
            var allProps = new Dictionary<string, object>();

            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    allProps.Add(prop.Key, prop.Value);
                }
            }

            if (_persistentProperties != null)
            {
                foreach (var prop in _persistentProperties)
                {
                    if (!allProps.ContainsKey(prop.Key))
                    {
                        allProps.Add(prop.Key, prop.Value);
                    }
                }
            }

            SendEvent(_userId, eventName, allProps, _sessionStartTime);
        }

        private static void SendEvent(
            string userId,
            string eventName,
            Dictionary<string, object> properties,
            long sessionStartTime = -1
        )
        {
            try
            {
                var eventData = new Dictionary<string, object>
                {
                    {"user_id", userId},
                    {"insert_id", Guid.NewGuid()},
                    {"event_type", eventName},
                    {"time", DateTimeOffset.Now.ToUnixTimeMilliseconds()},
                    {"event_properties", properties}
                };

                if (sessionStartTime > 0)
                {
                    eventData.Add("session_id", sessionStartTime);
                }
                
                var parameters = new Dictionary<string, string>
                {
                    {"api_key", _apiKey},
                    {"event", JsonSerializer.Serialize(eventData)}
                };
                
                var content = new FormUrlEncodedContent(parameters);
                Client.PostAsync(ApiAddress, content);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}