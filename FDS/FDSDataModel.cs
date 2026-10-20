using System.Collections.Generic;

public class FDSDataModel
{
    public List<FDSFrame> Frames = new List<FDSFrame>();

    public int FrameCount
    {
        get
        {
            return Frames.Count;
        }
    }

    public FDSFrame GetFrame(int index)
    {
        if (index < 0 || index >= Frames.Count)
            return null;

        return Frames[index];
    }

    public void Clear()
    {
        Frames.Clear();
    }

    public void AddFrame(FDSFrame frame)
    {
        Frames.Add(frame);
    }
}