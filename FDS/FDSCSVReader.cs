using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class FDSCSVReader : MonoBehaviour
{
    [Header("CSV Files")]
    public string hrrFile =
        "Market_hrr.csv";

    public string devcFile =
        "Market_devc.csv";

    [Header("Loaded Dataset")]
    public FDSDataModel dataModel;

    [Header("Loading State")]
    public bool isLoading;
    public bool isLoaded;
    public string lastError;

    private IEnumerator Start()
    {
        yield return LoadCSVCoroutine();
    }

    public void LoadCSV()
    {
        if (!isLoading)
        {
            StartCoroutine(
                LoadCSVCoroutine()
            );
        }
    }

    public IEnumerator LoadCSVCoroutine()
    {
        if (isLoading)
        {
            yield break;
        }

        isLoading = true;
        isLoaded = false;
        lastError = string.Empty;

        Debug.Log(
            "Loading FDS CSV files..."
        );

        string hrrPath =
            BuildStreamingAssetPath(
                hrrFile
            );

        string devcPath =
            BuildStreamingAssetPath(
                devcFile
            );

        Debug.Log(
            "HRR path: " + hrrPath
        );

        Debug.Log(
            "DEVC path: " + devcPath
        );

        string hrrText = null;
        string devcText = null;

        yield return LoadTextFile(
            hrrPath,
            result =>
            {
                hrrText = result;
            },
            error =>
            {
                lastError =
                    "HRR load error: " +
                    error;
            }
        );

        if (!string.IsNullOrEmpty(
                lastError))
        {
            Debug.LogError(lastError);

            isLoading = false;
            yield break;
        }

        yield return LoadTextFile(
            devcPath,
            result =>
            {
                devcText = result;
            },
            error =>
            {
                lastError =
                    "DEVC load error: " +
                    error;
            }
        );

        if (!string.IsNullOrEmpty(
                lastError))
        {
            Debug.LogError(lastError);

            isLoading = false;
            yield break;
        }

        dataModel =
            FDSCsvParser.LoadDatasetFromText(
                devcText,
                hrrText
            );

        if (dataModel == null ||
            dataModel.FrameCount == 0)
        {
            lastError =
                "FDS dataset could not be parsed " +
                "or contains no frames.";

            Debug.LogError(lastError);

            isLoading = false;
            yield break;
        }

        isLoaded = true;
        isLoading = false;

        Debug.Log(
            "======================================"
        );

        Debug.Log(
            "FDS DATASET LOADED SUCCESSFULLY"
        );

        Debug.Log(
            "Frames Loaded: " +
            dataModel.FrameCount
        );

        if (dataModel.FrameCount > 0)
        {
            FDSFrame first =
                dataModel.GetFrame(0);

            Debug.Log(
                "=========================="
            );

            Debug.Log(
                "FIRST FRAME"
            );

            Debug.Log(
                "Time : " +
                first.time
            );

            Debug.Log(
                "HRR  : " +
                first.hrr
            );

            Debug.Log(
                "------ DEVICES ------"
            );

            foreach (
                var device
                in first.deviceValues)
            {
                Debug.Log(
                    device.Key +
                    " : " +
                    device.Value
                );
            }

            Debug.Log(
                "=========================="
            );
        }

        Debug.Log(
            "======================================"
        );
    }

    private string BuildStreamingAssetPath(
        string fileName)
    {
        string path =
            Path.Combine(
                Application.streamingAssetsPath,
                fileName
            );

        return path.Replace(
            "\\",
            "/"
        );
    }

    private IEnumerator LoadTextFile(
        string path,
        System.Action<string> onSuccess,
        System.Action<string> onError)
    {
        using (
            UnityWebRequest request =
                UnityWebRequest.Get(path))
        {
            yield return
                request.SendWebRequest();

            if (request.result !=
                UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    request.error +
                    " | Path: " +
                    path
                );

                yield break;
            }

            string text =
                request.downloadHandler.text;

            if (string.IsNullOrWhiteSpace(
                    text))
            {
                onError?.Invoke(
                    "Downloaded file is empty. " +
                    "Path: " +
                    path
                );

                yield break;
            }

            onSuccess?.Invoke(text);
        }
    }

    public FDSFrame GetFrame(
        int index)
    {
        if (dataModel == null)
        {
            return null;
        }

        return dataModel.GetFrame(index);
    }

    public int FrameCount
    {
        get
        {
            if (dataModel == null)
            {
                return 0;
            }

            return dataModel.FrameCount;
        }
    }
}