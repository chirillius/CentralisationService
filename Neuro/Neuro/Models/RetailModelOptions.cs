namespace Neuro.Models;

public sealed class RetailModelOptions
{
    public bool UseStubFallback { get; set; } = true;
    public string ModelsRootPath { get; set; } = "DNNModels";
    public string ClientPresenceModelFileName { get; set; } = "yolo12m.onnx";
    public string PhoneModelFileName { get; set; } = "phone_best.onnx";
    public string BottlesModelFileName { get; set; } = "bottle_best.onnx";
    public int InputSize { get; set; } = 640;
    public double IouThreshold { get; set; } = 0.45;
    public double PresenceConfidenceThreshold { get; set; } = 0.25;
    public double PhoneConfidenceThreshold { get; set; } = 0.25;
    public double BottlesConfidenceThreshold { get; set; } = 0.25;
}
