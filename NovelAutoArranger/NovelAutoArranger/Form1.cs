using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.IO;


namespace NovelAutoArranger
{
    public class NovelInfo
    {
        public NovelInfo(FileInfo file, string folderName)
        {
            File = file;
            FolderName = folderName;
        }

        public NovelInfo(DirectoryInfo dir, string folderName)
        {
            Dir = dir;
            FolderName = folderName;
        }

        public void MoveTo(string dstPath)
        {
            if(File != null)
            {
                MoveFile(File, dstPath);
            }
            else if(Dir != null)
            {
                foreach(var file in Dir.GetFiles())
                {
                    string ext = Path.GetExtension(file.Name);

                    if (ext == ".zip")
                    {
                        MoveFile(file, dstPath);
                    }
                }

                if( Dir.GetFiles().Length <= 0 && Dir.GetDirectories().Length <= 0 )
                {
                    Dir.Delete();
                }
            }
        } 

        private void MoveFile(FileInfo file, string dstPath)
        {
            string fullName = Path.Combine(dstPath, file.Name);
            if (System.IO.File.Exists(fullName) == false)
            {
                file.MoveTo(fullName);
            }
        }

        public string FolderName;

        public FileInfo File = null;
        public DirectoryInfo Dir = null;
    }


    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            IniFile ini = new IniFile();
            // ini 읽기
            string iniPath = Path.Combine(Directory.GetCurrentDirectory(), "save.ini");
            if( File.Exists(iniPath) == false )
            {
                File.Create(iniPath);
            }

            ini.Load(iniPath);

            string waitingNovelPatch = ini["PathSetting"]["WaitingNovelPatch"].ToString();
            string managedNovelPath = ini["PathSetting"]["ManagedNovelPath"].ToString();

            if (waitingNovelPatch != null)
            {
                WatingNovelPath.Text = waitingNovelPatch;
            }

            if (managedNovelPath != null)
            {
                ManagedNovelPath.Text = managedNovelPath;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ApplyManagement(WatingNovelPath.Text, ManagedNovelPath.Text);

            IniFile ini = new IniFile();
            ini["PathSetting"]["WaitingNovelPatch"] = WatingNovelPath.Text;
            ini["PathSetting"]["ManagedNovelPath"] = ManagedNovelPath.Text;
            ini.Save(Path.Combine(Directory.GetCurrentDirectory(), "save.ini"));
        }

        private void ApplyManagement(string inputPath, string managedPath)
        {
            System.IO.DirectoryInfo inputDi = new System.IO.DirectoryInfo(inputPath);
            if (inputDi.Exists == false)
            {
                MessageBox.Show("입력 경로에 있는 것이 없습니다.", "error", MessageBoxButtons.OK);
                return;
            }

            DirectoryInfo managedTopDi = new DirectoryInfo(managedPath);
            List<DirectoryInfo> managedDiInfos = new List<DirectoryInfo>();

            if (managedTopDi.Exists)
            {
                DirectoryInfo[] diInfos = managedTopDi.GetDirectories("*", System.IO.SearchOption.AllDirectories);

                foreach(var diInfo in diInfos )
                {
                    managedDiInfos.Add(diInfo);
                }
            }

            List<NovelInfo> inputNovelInfos = new List<NovelInfo>();

            char[] buffer = new char[512];
            foreach (var fileInfo in inputDi.GetFiles())
            {
                //확장자 제거
                string folderName = GetFolderNameFromNovel(fileInfo.Name, buffer);

                if(folderName != null)
                    inputNovelInfos.Add(new NovelInfo(fileInfo, folderName));
            }

            foreach (var dirInfo in inputDi.GetDirectories())
            {
                //확장자 제거
                string folderName = GetFolderNameFromNovel(dirInfo.Name, buffer);

                if(folderName != null)
                    inputNovelInfos.Add(new NovelInfo(dirInfo, folderName));
            }


            foreach (var novelInfo in inputNovelInfos)
            {
                System.IO.DirectoryInfo dstDi = null;
                string dstPath = null;

                foreach (var managedDiInfo in managedDiInfos)
                {
                    float similarity = GetSimilarity(novelInfo.FolderName, managedDiInfo.Name);
                    if ( 0.6 < similarity)
                    {//파일이 있을 경우
                        dstDi = managedDiInfo;
                        break;
                    }
                }

                if (dstDi == null)
                {
                    dstPath = System.IO.Path.Combine(managedPath, novelInfo.FolderName);
                    dstDi = new System.IO.DirectoryInfo(dstPath);
                    dstDi.Create();
                    managedDiInfos.Add(dstDi);
                }
                else
                {
                    dstPath = dstDi.FullName;
                }

                novelInfo.MoveTo(dstPath);

                //string moveFullName = Path.Combine(dstPath, inputFileInfo.Key.Name);
                //if(File.Exists(moveFullName) == false)
                //{
                //    src.MoveTo(moveFullName);
                //}
            }
        }

        private string GetFolderNameFromNovel(string fileName, char[] buffer)
        {
            fileName = Path.GetFileNameWithoutExtension(fileName);

            //숫자 이후것들 제거
            int startIndex = (int)((float)fileName.Length * 0.5f);
            for (int i = startIndex; i < fileName.Length; ++i)
            {
                if (Char.IsNumber(fileName[i]) ||
                    fileName[i] == '@')
                {
                    fileName = fileName.Remove(i);
                    break;
                }
            }

            //괄호 제거
            int charCount = 0;
            bool bAddChar = true;
            for (int i = 0; i < fileName.Length; ++i)
            {
                char ch = fileName[i];
                if (ch == '-' /*|| ch == '.' || Char.IsNumber(ch)*/)
                    continue;

                if (ch == '외')
                {
                    int nextIndex = i + 1;
                    if (fileName.Length > nextIndex)
                    {
                        if (fileName[nextIndex] == '전')
                        {
                            ++i;
                            continue;
                        }
                    }
                }

                if ((ch == '(') || (ch == '['))
                {
                    bAddChar = false;
                }

                if (bAddChar)
                {
                    if( (ch != ')') && (ch != ']'))
                    {
                        buffer[charCount] = ch;
                        ++charCount;
                    }
                }

                if ((ch == ')') || (ch == ']'))
                {
                    bAddChar = true;
                }
            }

            if (charCount > 0)
            {
                char[] cBuffer = new char[charCount];
                Buffer.BlockCopy(buffer, 0, cBuffer, 0, charCount * 2);

                fileName = new string(cBuffer);
            }


            fileName = fileName.Trim();

            return fileName;
        }


        private int Min3(int a, int b, int c)
        {
            return System.Math.Min(System.Math.Min(a, b), c);
        }

        private int ComputeDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] distance = new int[n + 1, m + 1]; // matrix
            int cost = 0;
            if (n == 0) return m;
            if (m == 0) return n;
            //init1
            for (int i = 0; i <= n; distance[i, 0] = i++) ;
            for (int j = 0; j <= m; distance[0, j] = j++) ;
            //find min distance
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    cost = (t.Substring(j - 1, 1) ==
                        s.Substring(i - 1, 1) ? 0 : 1);
                    distance[i, j] = Min3(distance[i - 1, j] + 1,
                    distance[i, j - 1] + 1,
                    distance[i - 1, j - 1] + cost);
                }
            }
            return distance[n, m];
        }

        public float GetSimilarity(string string1, string string2)
        {
            float dis = ComputeDistance(string1, string2);
            float maxLen = string1.Length;
            if (maxLen < string2.Length)
                maxLen = string2.Length;
            if (maxLen == 0.0F)
                return 1.0F;
            else
                return 1.0F - dis / maxLen;
        }
    }
}
