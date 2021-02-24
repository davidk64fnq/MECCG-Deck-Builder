using System;
using System.Collections.Generic;
using System.IO;

namespace MECCG_Deck_Builder
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class TabStates
    {
    }

    public class Transform
    {
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double PosZ { get; set; }
        public double RotX { get; set; }
        public double RotY { get; set; }
        public double RotZ { get; set; }
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
        public double ScaleZ { get; set; }
    }

    public class ColorDiffuse
    {
        public double R { get; set; }
        public double G { get; set; }
        public double B { get; set; }
    }

    public class CD3
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD126
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD136
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD8
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD7
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD129
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD132
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD5
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD6
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD4
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD128
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD2
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CD1
    {
        public string FaceURL { get; set; }
        public string BackURL { get; set; }
        public int NumWidth { get; set; }
        public int NumHeight { get; set; }
        public bool BackIsHidden { get; set; }
        public bool UniqueBack { get; set; }
        public int Type { get; set; }
    }

    public class CustomDeck
    {
        public CD3 CD3 { get; set; }
        public CD126 CD126 { get; set; }
        public CD136 CD136 { get; set; }
        public CD8 CD8 { get; set; }
        public CD7 CD7 { get; set; }
        public CD129 CD129 { get; set; }
        public CD132 CD132 { get; set; }
        public CD5 CD5 { get; set; }
        public CD6 CD6 { get; set; }
        public CD4 CD4 { get; set; }
        public CD128 CD128 { get; set; }
        public CD2 CD2 { get; set; }
        public CD1 CD1 { get; set; }
    }

    public class ContainedObject
    {
        public string Name { get; set; }
        public Transform Transform { get; set; }
        public string Nickname { get; set; }
        public string Description { get; set; }
        public string GMNotes { get; set; }
        public ColorDiffuse ColorDiffuse { get; set; }
        public bool Locked { get; set; }
        public bool Grid { get; set; }
        public bool Snap { get; set; }
        public bool IgnoreFoW { get; set; }
        public bool MeasureMovement { get; set; }
        public bool DragSelectable { get; set; }
        public bool Autoraise { get; set; }
        public bool Sticky { get; set; }
        public bool Tooltip { get; set; }
        public bool GridProjection { get; set; }
        public bool HideWhenFaceDown { get; set; }
        public bool Hands { get; set; }
        public int CardID { get; set; }
        public bool SidewaysCard { get; set; }
        public CustomDeck CustomDeck { get; set; }
        public string LuaScript { get; set; }
        public string LuaScriptState { get; set; }
        public string XmlUI { get; set; }
        public string GUID { get; set; }
    }

    public class ObjectState
    {
        public string Name { get; set; }
        public Transform Transform { get; set; }
        public string Nickname { get; set; }
        public string Description { get; set; }
        public string GMNotes { get; set; }
        public ColorDiffuse ColorDiffuse { get; set; }
        public bool Locked { get; set; }
        public bool Grid { get; set; }
        public bool Snap { get; set; }
        public bool IgnoreFoW { get; set; }
        public bool MeasureMovement { get; set; }
        public bool DragSelectable { get; set; }
        public bool Autoraise { get; set; }
        public bool Sticky { get; set; }
        public bool Tooltip { get; set; }
        public bool GridProjection { get; set; }
        public bool HideWhenFaceDown { get; set; }
        public bool Hands { get; set; }
        public bool SidewaysCard { get; set; }
        public List<int> DeckIDs { get; set; }
        public CustomDeck CustomDeck { get; set; }
        public string LuaScript { get; set; }
        public string LuaScriptState { get; set; }
        public string XmlUI { get; set; }
        public List<ContainedObject> ContainedObjects { get; set; }
        public string GUID { get; set; }
    }

    public class Root
    {
        public string SaveName { get; set; }
        public string GameMode { get; set; }
        public string Date { get; set; }
        public double Gravity { get; set; }
        public double PlayArea { get; set; }
        public string GameType { get; set; }
        public string GameComplexity { get; set; }
        public List<object> Tags { get; set; }
        public string Table { get; set; }
        public string Sky { get; set; }
        public string Note { get; set; }
        public string Rules { get; set; }
        public TabStates TabStates { get; set; }
        public List<ObjectState> ObjectStates { get; set; }
        public string LuaScript { get; set; }
        public string LuaScriptState { get; set; }
        public string XmlUI { get; set; }
        public string VersionNumber { get; set; }
    }


}
