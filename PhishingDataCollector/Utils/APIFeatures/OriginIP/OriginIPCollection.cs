using System;

public class OriginIP : URLObject
{
    public string Origin { set; get; }
    public OriginIP (string server,  string origin) : base (server)
    {
        Origin = origin;
    }
}

public class OriginIPCollection : URLsCollection
{
    // Searches for the IP address in the collection and returns true if found, false otherwise
    // If the checkVal flag is set to true, the function returns true only if
    // the IP is found and the origin has a value different from empty
    public bool Contains(OriginIP item, bool checkVal=false) 
    {
        foreach (OriginIP ip_obj in innerCol)
        {
            if (ip_obj.Address == item.Address)
            {
                if (checkVal)
                {
                    if (!string.IsNullOrEmpty(item.Origin))
                    {
                        return true;
                    }
                } else
                {
                    return true;
                }
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
                item.Address, item.Origin);
        }
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
            array[i + arrayIndex] = (OriginIP) innerCol[i];
        }
    }
}
