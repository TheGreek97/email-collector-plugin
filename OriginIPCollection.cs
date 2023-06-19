using System;
using System.Collections;
using System.Collections.Generic;

namespace PhishingDataCollector;


public class IPLocalization
{

    private const string _api_key = "bdc_cd8f72a03c1843d59d402d7cdd1b0a6b";
    private const string _api_request_url = "https://api.bigdatacloud.net/data/country-by-ip";
    private string _ip_addr_request;

    private string country_name { get; set; }
    private string region_name { get; set; }


    public IPLocalization(string origin_ip)
    {

        if (string.IsNullOrEmpty(origin_ip))
        {
            SetUnknown();
        }
        else
        {
            _ip_addr_request = origin_ip;
        }
    }

    public async void PerformAPICall()
    {
        //perform API call to discover the origin (https://www.bigdatacloud.com/docs/ip-geolocation)
        var queryParameters = new Dictionary<string, string>()
        {
            ["ip"] = _ip_addr_request,
            ["key"] = _api_key
        };
        var api_url = QueryHelpers.AddQueryString(_api_request_url, queryParameters);

        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var response = httpClient.GetAsync(api_url).Result;
        if (response.IsSuccessStatusCode)
        {
            string resultString = response.Content.ReadAsStringAsync().Result;
            try
            {
                JObject jsonObject = (JObject)JsonConvert.DeserializeObject(resultString);
                JObject country = (JObject)jsonObject.GetValue("country");
                if (country != null)
                {
                    country_name = (string)country.GetValue("name");
                    region_name = (string)((JObject)country.GetValue("wbRegion")).GetValue("value");
                }
                else
                {
                    country_name = "unknown";
                    region_name = "unknown";
                }
            }
            catch (Exception ex) // when (ex is JsonException || ex is KeyNotFoundException)
            {
                Debug.WriteLine(ex);
            }
        }
        else
        {
            SetUnknown();
        }
    }

    public string GetFeature()
    {
        if (region_name == "Italy" || region_name == "Russia") { return country_name; }
        else if (region_name == "" && country_name == "") { return "unknown"; }
        else { return region_name; }
    }

    private void SetUnknown()
    {
        country_name = string.Empty;
        region_name = string.Empty;
    }
}


public class OriginIPCollection : ICollection<OriginIP>
{
    public IEnumerator<OriginIP> GetEnumerator()
    {
        return new OriginIPEnumerator(this);
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return new OriginIPEnumerator(this);
    }

    // The inner collection to store objects.
    private List<OriginIP> innerCol;

    public OriginIPCollection()
    {
        innerCol = new List<OriginIP>();
    }

    // Adds an index to the collection.
    public OriginIP this[int index]
    {
        get { return (OriginIP)innerCol[index]; }
        set { innerCol[index] = value; }
    }

    // Determines if an item is in the collection
    // by searching for the IP address.
    public bool Contains(OriginIP item)
    {
        foreach (OriginIP ip_obj in innerCol)
        {
            if (ip_obj.IP == item.IP)
            {
                return true;
            }
        }
        return false;
    }

    // Adds an item if it is not already in the collection
    // as determined by calling the Contains method.
    public void Add(OriginIP item)
    {

        if (!Contains(item))
        {
            innerCol.Add(item);
        }
        else
        {
            Console.WriteLine("The IP {0} was already added to the collection (origin = {1}).",
                item.IP, item.origin);
        }
    }

    public void Clear()
    {
        innerCol.Clear();
    }

    public void CopyTo(OriginIP[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException("The array cannot be null.");
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException("The starting array index cannot be negative.");
        if (Count > array.Length - arrayIndex)
            throw new ArgumentException("The destination array has fewer elements than the collection.");

        for (int i = 0; i < innerCol.Count; i++)
        {
            array[i + arrayIndex] = innerCol[i];
        }
    }

    public int Count
    {
        get
        {
            return innerCol.Count;
        }
    }

    public bool IsReadOnly
    {
        get { return false; }
    }

    public bool Remove(OriginIP item)
    {
        bool result = false;

        // Iterate the inner collection to
        // find the IP to be removed.
        for (int i = 0; i < innerCol.Count; i++)
        {

            OriginIP curIP = (OriginIP)innerCol[i];

            if (curIP.IP == item.IP))
            {
                innerCol.RemoveAt(i);
                result = true;
                break;
            }
        }
        return result;
    }
}

public class OriginIP
{
    public string IP { set; get; }
    public string origin { set; get; }
}

public class OriginIPEnumerator : IEnumerator<OriginIP>
{
    private OriginIPCollection _collection;
    private int curIndex;
    private OriginIP curIP;

    public OriginIPEnumerator(OriginIPCollection collection)
    {
        _collection = collection;
        curIndex = -1;
        curIP = default(OriginIP);
    }

    public bool MoveNext()
    {
        //Avoids going beyond the end of the collection.
        if (++curIndex >= _collection.Count)
        {
            return false;
        }
        else
        {
            // Set current box to next item in collection.
            curIP = _collection[curIndex];
        }
        return true;
    }

    public void Reset() { curIndex = -1; }

    void IDisposable.Dispose() { }

    public OriginIP Current
    {
        get { return curIP; }
    }

    object IEnumerator.Current
    {
        get { return Current; }
    }
}