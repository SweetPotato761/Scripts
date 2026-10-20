using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class FDSCsvParser
{
    private const float TimeKeyScale = 1000f;

    public static FDSDataModel LoadDatasetFromText(
        string devcCsvText,
        string hrrCsvText)
    {
        if (string.IsNullOrWhiteSpace(devcCsvText))
        {
            Debug.LogError(
                "FDSCsvParser: DEVC CSV text is empty."
            );

            return null;
        }

        if (string.IsNullOrWhiteSpace(hrrCsvText))
        {
            Debug.LogError(
                "FDSCsvParser: HRR CSV text is empty."
            );

            return null;
        }

        Dictionary<int, FDSFrame> frameMap =
            new Dictionary<int, FDSFrame>();

        ParseDeviceCsvText(
            devcCsvText,
            frameMap
        );

        ParseHrrCsvText(
            hrrCsvText,
            frameMap
        );

        List<int> sortedKeys =
            new List<int>(frameMap.Keys);

        sortedKeys.Sort();

        FDSDataModel dataset =
            new FDSDataModel();

        foreach (int key in sortedKeys)
        {
            FDSFrame frame =
                frameMap[key];

            frame.time =
                key / TimeKeyScale;

            dataset.AddFrame(frame);
        }

        Debug.Log(
            "FDS dataset loaded. Frames: " +
            dataset.FrameCount
        );

        if (dataset.FrameCount > 0)
        {
            LogDatasetCheck(dataset);
        }

        return dataset;
    }

    private static void ParseDeviceCsvText(
        string csvText,
        Dictionary<int, FDSFrame> frameMap)
    {
        string[] lines =
            SplitTextIntoLines(csvText);

        if (lines.Length < 3)
        {
            Debug.LogError(
                "DEVC CSV is too short or invalid."
            );

            return;
        }

        string[] headers =
            SplitLine(lines[1]);

        int timeIndex =
            FindColumn(
                headers,
                "Time"
            );

        if (timeIndex < 0)
        {
            timeIndex = 0;
        }

        for (int row = 2;
             row < lines.Length;
             row++)
        {
            if (string.IsNullOrWhiteSpace(
                    lines[row]))
            {
                continue;
            }

            string[] columns =
                SplitLine(lines[row]);

            if (columns.Length <= timeIndex)
            {
                continue;
            }

            float rawTime =
                ParseFloatSafe(
                    columns[timeIndex]
                );

            int timeKey =
                CreateTimeKey(rawTime);

            FDSFrame frame =
                GetOrCreateFrame(
                    frameMap,
                    timeKey
                );

            for (int column = 0;
                 column < headers.Length &&
                 column < columns.Length;
                 column++)
            {
                if (column == timeIndex)
                {
                    continue;
                }

                string deviceId =
                    CleanHeader(
                        headers[column]
                    );

                if (string.IsNullOrWhiteSpace(
                        deviceId))
                {
                    continue;
                }

                float value =
                    ParseFloatSafe(
                        columns[column]
                    );

                frame.deviceValues[deviceId] =
                    value;

                if (string.Equals(
                        deviceId,
                        "TEMP_EXIT",
                        StringComparison.OrdinalIgnoreCase))
                {
                    frame.tempExit =
                        value;
                }
                else if (string.Equals(
                             deviceId,
                             "TEMP_CENTER",
                             StringComparison.OrdinalIgnoreCase))
                {
                    frame.tempCenter =
                        value;
                }
            }
        }
    }

    private static void ParseHrrCsvText(
        string csvText,
        Dictionary<int, FDSFrame> frameMap)
    {
        string[] lines =
            SplitTextIntoLines(csvText);

        if (lines.Length < 3)
        {
            Debug.LogError(
                "HRR CSV is too short or invalid."
            );

            return;
        }

        string[] headers =
            SplitLine(lines[1]);

        int timeIndex =
            FindColumn(
                headers,
                "Time"
            );

        int hrrIndex =
            FindExactColumn(
                headers,
                "HRR"
            );

        if (timeIndex < 0)
        {
            timeIndex = 0;
        }

        if (hrrIndex < 0)
        {
            Debug.LogError(
                "HRR column was not found in HRR CSV."
            );

            return;
        }

        for (int row = 2;
             row < lines.Length;
             row++)
        {
            if (string.IsNullOrWhiteSpace(
                    lines[row]))
            {
                continue;
            }

            string[] columns =
                SplitLine(lines[row]);

            if (columns.Length <= timeIndex ||
                columns.Length <= hrrIndex)
            {
                continue;
            }

            float rawTime =
                ParseFloatSafe(
                    columns[timeIndex]
                );

            int timeKey =
                CreateTimeKey(rawTime);

            FDSFrame frame =
                GetOrCreateFrame(
                    frameMap,
                    timeKey
                );

            frame.hrr =
                ParseFloatSafe(
                    columns[hrrIndex]
                );
        }
    }

    private static string[] SplitTextIntoLines(
        string text)
    {
        return text.Split(
            new[]
            {
                "\r\n",
                "\n",
                "\r"
            },
            StringSplitOptions.None
        );
    }

    private static FDSFrame GetOrCreateFrame(
        Dictionary<int, FDSFrame> frameMap,
        int timeKey)
    {
        if (frameMap.TryGetValue(
                timeKey,
                out FDSFrame existingFrame))
        {
            return existingFrame;
        }

        FDSFrame newFrame =
            new FDSFrame
            {
                time =
                    timeKey / TimeKeyScale,

                hrr = 0f
            };

        frameMap.Add(
            timeKey,
            newFrame
        );

        return newFrame;
    }

    private static int CreateTimeKey(
        float time)
    {
        return Mathf.RoundToInt(
            time * TimeKeyScale
        );
    }

    private static int FindColumn(
        string[] headers,
        string requiredText)
    {
        for (int i = 0;
             i < headers.Length;
             i++)
        {
            string header =
                CleanHeader(
                    headers[i]
                );

            if (header.IndexOf(
                    requiredText,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindExactColumn(
        string[] headers,
        string requiredName)
    {
        for (int i = 0;
             i < headers.Length;
             i++)
        {
            string header =
                CleanHeader(
                    headers[i]
                );

            if (string.Equals(
                    header,
                    requiredName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string CleanHeader(
        string header)
    {
        if (string.IsNullOrWhiteSpace(
                header))
        {
            return string.Empty;
        }

        return header
            .Trim()
            .Replace("\"", "");
    }

    private static float ParseFloatSafe(
        string valueText)
    {
        if (string.IsNullOrWhiteSpace(
                valueText))
        {
            return 0f;
        }

        string cleaned =
            valueText
                .Trim()
                .Replace("\"", "");

        if (float.TryParse(
                cleaned,
                NumberStyles.Float |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out float value))
        {
            return value;
        }

        return 0f;
    }

    private static string[] SplitLine(
        string line)
    {
        if (line.Contains(","))
        {
            return line.Split(',');
        }

        if (line.Contains("\t"))
        {
            return line.Split('\t');
        }

        return line.Split(
            new[] { ' ' },
            StringSplitOptions.RemoveEmptyEntries
        );
    }

    private static void LogDatasetCheck(
        FDSDataModel dataset)
    {
        int testIndex =
            Mathf.Min(
                4,
                dataset.FrameCount - 1
            );

        FDSFrame frame =
            dataset.GetFrame(testIndex);

        if (frame == null)
        {
            return;
        }

        frame.deviceValues.TryGetValue(
            "SMOKE_01",
            out float smoke01
        );

        frame.deviceValues.TryGetValue(
            "SMOKE_02",
            out float smoke02
        );

        frame.deviceValues.TryGetValue(
            "SMOKE_03",
            out float smoke03
        );

        Debug.Log(
            "Merged frame check | " +
            "Time: " +
            frame.time.ToString("F3") +
            " | HRR: " +
            frame.hrr.ToString("F1") +
            " | SMOKE_01: " +
            smoke01.ToString("F4") +
            " | SMOKE_02: " +
            smoke02.ToString("F4") +
            " | SMOKE_03: " +
            smoke03.ToString("F4")
        );
    }
}