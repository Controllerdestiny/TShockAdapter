namespace MorMorAdapter.Model;

internal class Item
{
    public int netID { get; set; }

    public int prefix { get; set; }

    public int stack { get; set; }

    public Item(int netID, int prefix, int stack)
    {
        this.netID = netID;
        this.prefix = prefix;
        this.stack = stack;
    }
}
