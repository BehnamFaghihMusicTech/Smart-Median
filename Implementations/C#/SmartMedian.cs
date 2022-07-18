using System;
using System.Collections.Generic;
using System.Linq;

namespace ContourSmoother
{
    /// <summary>
    /// This class includes the required fields for estimated fundamental frequencies. 
    /// </summary>
    public class PitchFrame
    {
        /// <summary>
        /// This filed indicates the time of the estimated F0 in second.
        /// </summary>
        public double? TimeSecond;
        /// <summary>
        /// This filed contains the fundamental frequency in Hertz.
        /// </summary>
        public double? F0Hz;
        /// <summary>
        /// This field show the amplitude of the estimated F0.
        /// </summary>
        public double? Amplitude;
    }

    /// <summary>
    /// This algorithm is designed for smoothing singing pitch contour. This is using Smart-Median algorithm for smoothing.
    /// The details of the algorithm discussed in the following scientific paper:
    /// Smart-Median: A new real-time algorithm for smoothing singing pitch contour.
    ///  </summary>
    public class SmartMedian
    {
        /// <summary>
        /// This method smooths the series of data sent to it by employing smart-median algorithm.
        /// </summary>
        /// <param name="data">The list of the estimated pitches that need to be smoothed</param>
        /// <param name="maxFrequency">The maximum acceptable frequency</param>
        /// <param name="priorDistance">this indicates how many estimated pitches before the current pitch frequency should be considered for
        /// the median</param>
        /// <param name="followingDistance">Indicating how many estimated pitches after the current pitch frequency should be considered for
        /// the median</param>
        /// <param name="acceptableFrequencyDifference">indicates the maximum pitch frequency interval acceptable for jumping between two consequent detected pitches. </param>
        /// <param name="noZero">this is the minimum number of consequent zero pitch frequencies that should be considered a
        /// correctly estimated silence/rest. </param>
        /// <returns>A list of smoothed data</returns>
        public static List<PitchFrame> Smoother(List<PitchFrame> data, double maxFrequency = 1200, int priorDistance = 4,
           int followingDistance = 4, double acceptableFrequencyDifference = 100, int noZero = 4)
        {
            if (data[0].F0Hz > maxFrequency)
            {
                PitchFrame pitchFrame = new PitchFrame()
                {
                    F0Hz = 0,
                    Amplitude = data[0].Amplitude,
                    TimeSecond = data[0].TimeSecond
                };
                data[0] = pitchFrame;
            }

            for (int i = 1; i < data.Count; i++)
            {
                if (data[i].F0Hz == null)
                    continue;

                double? currentFrequency = data[i].F0Hz;
                double? currentAmplitude = data[i].Amplitude;
                double? previousFrequency = data[i - 1].F0Hz;

                int countZeros = CountZeros(data, i);

                if (previousFrequency > 0)
                {
                    if (Math.Abs(Convert.ToDouble(currentFrequency - previousFrequency)) > acceptableFrequencyDifference)
                    {
                        if (countZeros < noZero)
                        {
                            double? median;
                            int fd = followingDistance;
                            do
                            {
                                List<double?> localPitches = GetLocalPitches(data, i - priorDistance, i + fd);
                                localPitches = localPitches.Where(x => x != 0).ToList();
                                median = Median(localPitches);
                                fd--;
                            } while (Math.Abs(Convert.ToDouble(median - previousFrequency)) >
                                     acceptableFrequencyDifference &&
                                     fd >= 0);

                            PitchFrame newPitchFrame = new PitchFrame
                            {
                                F0Hz = median < maxFrequency ? median : 0,
                                Amplitude = currentAmplitude,
                                TimeSecond = data[i].TimeSecond
                            };
                            data[i] = newPitchFrame;
                        }
                    }
                    else if (currentFrequency == 0)
                    {
                        if (countZeros > noZero)
                        {
                            PitchFrame newPitchFrame = new PitchFrame
                            {
                                F0Hz = 0,
                                Amplitude = currentAmplitude,
                                TimeSecond = data[i].TimeSecond
                            };
                            data[i] = newPitchFrame;
                        }
                        else if (currentFrequency == 0.0 && countZeros < noZero)
                        {
                            for (int j = i; j <= i + countZeros && j < data.Count; j++)
                            {
                                double? median;
                                int fd = followingDistance;
                                do
                                {
                                    List<double?> localPitches = GetLocalPitches(data, j - priorDistance, j + fd);
                                    median = Median(localPitches);
                                    fd--;
                                } while (Math.Abs(Convert.ToDouble(median - previousFrequency)) >
                                         acceptableFrequencyDifference &&
                                         fd >= 0);


                                PitchFrame newPitchFrame = new PitchFrame
                                {
                                    F0Hz = median < maxFrequency ? median : 0,
                                    Amplitude = currentAmplitude,
                                    TimeSecond = data[j].TimeSecond
                                };
                                data[j] = newPitchFrame;
                            }

                            i += countZeros;
                        }
                    }
                    else if (currentFrequency > maxFrequency)
                    {
                        PitchFrame newPitchFrame = new PitchFrame()
                        {

                            F0Hz = currentFrequency < maxFrequency ? currentFrequency : 0,
                            Amplitude = currentAmplitude,
                            TimeSecond = data[i].TimeSecond
                        };
                        data[i] = newPitchFrame;
                    }
                }
                else if (previousFrequency == 0.0)
                {
                    if (currentFrequency > maxFrequency ||
                        (i < data.Count - 1 && data[i + 1].F0Hz != null &&
                         Math.Abs(Convert.ToDouble(currentFrequency - data[i + 1].F0Hz)) >
                         acceptableFrequencyDifference))
                    {
                        PitchFrame newPitchFrame = new PitchFrame
                        {
                            F0Hz = 0,
                            Amplitude = currentAmplitude,
                            TimeSecond = data[i].TimeSecond
                        };
                        data[i] = newPitchFrame;
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// This method counts the number of consecutively estimated zero pitch frequencies in data after parameter fromIndex.
        /// </summary>
        /// <param name="data">this field includes all the estimated F0s.</param>
        /// <param name="fromIndex">Indicates start index that should start counting zeros.</param>
        /// <returns>return the number of consecutively zero frequencies</returns>
        public static int CountZeros(List<PitchFrame> data, int fromIndex)
        {
            int count = 0;
            int indexOfFirstZero = data.Skip(fromIndex).ToList().FindIndex(t => Convert.ToInt32(t.F0Hz) == 0);
            int indexOfLastZero = data.Skip(fromIndex).ToList().FindIndex(t => t.F0Hz > 0);
            indexOfFirstZero += fromIndex;
            if (indexOfLastZero == -1)
                indexOfLastZero = data.Count() - 1;
            else
                indexOfLastZero += fromIndex;
            count = indexOfLastZero - indexOfFirstZero;

            return (count > 0 ? count : 0);
        }

        /// <summary>
        /// This method extract the local estimated pitches
        /// </summary>
        /// <param name="data">The list of all the pitch frames.</param>
        /// <param name="startIndex">Indicates the start index to extract.</param>
        /// <param name="endIndex">Indicate the last index to extract.</param>
        /// <returns></returns>
        private static List<double?> GetLocalPitches(List<PitchFrame> data, int startIndex, int endIndex)
        {
            List<double?> localPitches =
                data.Skip(startIndex).Take(endIndex - startIndex + 1).Select(t => t.F0Hz).ToList();

            return localPitches;
        }

        /// <summary>
        /// This method calculate the standard median filter.
        /// </summary>
        /// <param name="data">The list of the data that their median should be calculated.</param>
        /// <returns>The median of the data</returns>
        public static double? Median(List<double?> data)
        {
            data.Sort();
            double? med = 0;
            if (data.Count % 2 == 0)
                med = (data[data.Count / 2] + data[(data.Count / 2) - 1]) / 2;
            else
                med = data[data.Count / 2];
            return med;
        }
    }
}
