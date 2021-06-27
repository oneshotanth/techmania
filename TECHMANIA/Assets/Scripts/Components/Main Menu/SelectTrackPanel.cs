﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectTrackPanel : MonoBehaviour
{
    protected class Subfolder
    {
        public string path;
        public string eyecatchFullPath;
    }
    protected class TrackInFolder
    {
        // Doesn't include the track itself.
        public string folder;
        public Track track;
    }
    protected class ErrorInTrack
    {
        public string trackFile;
        public string message;
    }

    protected static string currentLocation;
    // Cached, keyed by track folder's parent folder.
    protected static Dictionary<string, List<Subfolder>> subfolderList;
    protected static Dictionary<string, List<TrackInFolder>> trackList;
    protected static Dictionary<string, List<ErrorInTrack>>
        errorTrackList;
    static SelectTrackPanel()
    {
        currentLocation = "";
        subfolderList = new Dictionary<string, List<Subfolder>>();
        trackList = new Dictionary<string, List<TrackInFolder>>();
        errorTrackList = new Dictionary<string, List<ErrorInTrack>>();
    }

    public static void RemoveCachedListsAtCurrentLocation()
    {
        subfolderList.Remove(currentLocation);
        trackList.Remove(currentLocation);
        errorTrackList.Remove(currentLocation);
    }

    public Button backButton;
    public Button trackFilterButton;
    public Button refreshButton;
    public Button goUpButton;
    public TextMeshProUGUI locationDisplay;
    public GridLayoutGroup subfolderGrid;
    public GameObject subfolderCardTemplate;
    public GridLayoutGroup trackGrid;
    public GameObject trackCardTemplate;
    public GameObject errorCardTemplate;
    public GameObject newTrackCard;
    public TextMeshProUGUI trackListBuildingProgress;
    public GameObject trackStatusText;
    public TrackFilterSidesheet trackFilterSidesheet;
    public Panel selectPatternPanel;
    public MessageDialog messageDialog;

    protected Dictionary<GameObject, string> cardToSubfolder;
    protected Dictionary<GameObject, TrackInFolder> cardToTrack;
    protected Dictionary<GameObject, string> cardToError;

    private void Start()
    {
        // Reset the keyword every time the main menu or editor scene
        // loads.
        trackFilterSidesheet.ResetSearchKeyword();
    }

    private void OnEnable()
    {
        StartCoroutine(Refresh());
        TrackFilterSidesheet.trackFilterChanged += 
            OnTrackFilterChanged;
    }

    private void OnDisable()
    {
        TrackFilterSidesheet.trackFilterChanged -=
            OnTrackFilterChanged;
    }

    protected IEnumerator Refresh()
    {
        // Initialization and/or disaster recovery.
        if (currentLocation == "" ||
            !Directory.Exists(currentLocation))
        {
            currentLocation = Paths.GetTrackRootFolder();
        }

        // Show location.
        locationDisplay.text = currentLocation;

        // Activate all grids regardless of content.
        subfolderGrid.gameObject.SetActive(true);
        trackGrid.gameObject.SetActive(true);

        // Remove all objects from grid, except for templates.
        for (int i = 0; i < subfolderGrid.transform.childCount; i++)
        {
            GameObject o = subfolderGrid.transform.GetChild(i)
                .gameObject;
            if (o == subfolderCardTemplate) continue;
            Destroy(o);
        }
        for (int i = 0; i < trackGrid.transform.childCount; i++)
        {
            GameObject o = trackGrid.transform.GetChild(i).gameObject;
            if (o == trackCardTemplate) continue;
            if (o == errorCardTemplate) continue;
            if (o == newTrackCard) continue;
            Destroy(o);
        }

        if (!trackList.ContainsKey(currentLocation))
        {
            backButton.interactable = false;
            trackFilterButton.interactable = false;
            refreshButton.interactable = false;
            goUpButton.interactable = false;
            newTrackCard.gameObject.SetActive(false);
            trackListBuildingProgress.gameObject.SetActive(true);
            trackStatusText.SetActive(false);

            // Launch background worker to rebuild track list.
            trackListBuilder = new BackgroundWorker();
            trackListBuilder.DoWork += TrackListBuilderDoWork;
            trackListBuilder.RunWorkerCompleted +=
                TrackListBuilderCompleted;
            builderDone = false;
            builderProgress = "";
            Options.TemporarilyDisableVSync();
            trackListBuilder.RunWorkerAsync();
            do
            {
                trackListBuildingProgress.text =
                    builderProgress;
                yield return null;
            } while (!builderDone);
            Options.RestoreVSync();

            trackListBuildingProgress.gameObject.SetActive(false);
            backButton.interactable = true;
            trackFilterButton.interactable = true;
            refreshButton.interactable = true;
        }

        // Enable go up button if applicable.
        goUpButton.interactable = currentLocation !=
            Paths.GetTrackRootFolder();

        // Instantiate subfolder cards.
        cardToSubfolder = new Dictionary<GameObject, string>();
        GameObject firstCard = null;
        bool subfolderGridEmpty = true;
        bool trackGridEmpty = true;
        foreach (Subfolder subfolder in subfolderList[currentLocation])
        {
            GameObject card = Instantiate(subfolderCardTemplate,
                subfolderGrid.transform);
            card.name = "Subfolder Card";
            card.GetComponent<SubfolderCard>().Initialize(
                new DirectoryInfo(subfolder.path).Name,
                subfolder.eyecatchFullPath);
            card.SetActive(true);
            subfolderGridEmpty = false;

            // Record mapping.
            cardToSubfolder.Add(card, subfolder.path);

            // Bind click event.
            card.GetComponent<Button>().onClick.AddListener(() =>
            {
                OnSubfolderCardClick(card);
            });

            if (firstCard == null)
            {
                firstCard = card;
            }
        }

        // Make a local copy of the track list so we can apply
        // custom sorting.
        List<TrackInFolder> sortedTracks = new List<TrackInFolder>();
        foreach (TrackInFolder t in trackList[currentLocation])
        {
            sortedTracks.Add(t);
        }
        sortedTracks.Sort(
            (TrackInFolder t1, TrackInFolder t2) =>
            {
                return string.Compare(t1.track.trackMetadata.title,
                    t2.track.trackMetadata.title);
            });

        // Instantiate track cards.
        cardToTrack = new Dictionary<GameObject, TrackInFolder>();
        foreach (TrackInFolder trackInFolder in sortedTracks)
        {
            GameObject card = Instantiate(trackCardTemplate,
                trackGrid.transform);
            card.name = "Track Card";
            card.GetComponent<TrackCard>().Initialize(
                trackInFolder.folder,
                trackInFolder.track.trackMetadata);
            card.SetActive(true);
            trackGridEmpty = false;

            // Record mapping.
            cardToTrack.Add(card, trackInFolder);

            // Bind click event.
            card.GetComponent<Button>().onClick.AddListener(() =>
            {
                OnTrackCardClick(card);
            });

            if (firstCard == null)
            {
                firstCard = card;
            }
        }

        // Instantiate error cards.
        cardToError = new Dictionary<GameObject, string>();
        foreach (ErrorInTrack error in 
            errorTrackList[currentLocation])
        {
            GameObject card = null;
            string message = Locale.GetStringAndFormat(
                "select_track_error_format",
                error.trackFile,
                error.message);

            // Instantiate card.
            card = Instantiate(errorCardTemplate, 
                trackGrid.transform);
            card.name = "Error Card";
            card.SetActive(true);
            trackGridEmpty = false;

            // Record mapping.
            cardToError.Add(card, message);

            // Bind click event.
            card.GetComponent<Button>().onClick.AddListener(() =>
            {
                OnErrorCardClick(card);
            });

            if (firstCard == null)
            {
                firstCard = card;
            }
        }

        if (ShowNewTrackCard())
        {
            newTrackCard.transform.SetAsLastSibling();
            newTrackCard.SetActive(true);
            trackGridEmpty = false;
            newTrackCard.GetComponent<Button>().onClick
                .RemoveAllListeners();
            newTrackCard.GetComponent<Button>().onClick
                .AddListener(() =>
            {
                OnNewTrackCardClick();
            });

            if (firstCard == null)
            {
                firstCard = newTrackCard;
            }
        }

        // Deactivate empty grids.
        subfolderGrid.gameObject.SetActive(!subfolderGridEmpty);
        trackGrid.gameObject.SetActive(!trackGridEmpty);

        if (!trackFilterSidesheet.gameObject.activeSelf)
        {
            if (firstCard == null)
            {
                EventSystem.current.SetSelectedGameObject(
                    backButton.gameObject);
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(firstCard);
            }
        }
        
        // TODO: show "no track" message and "tracks hidden" message
        // in this.
        trackStatusText.SetActive(firstCard == null);
    }

    protected virtual bool ShowNewTrackCard()
    {
        return false;
    }

    private void Update()
    {
        // Synchronize alpha with sidesheet because the
        // CanvasGroup on the sidesheet ignores parent.
        if (PanelTransitioner.transitioning &&
            trackFilterSidesheet.gameObject.activeSelf)
        {
            trackFilterSidesheet.GetComponent<CanvasGroup>().alpha
                = GetComponent<CanvasGroup>().alpha;
        }
    }

    #region Track filter side sheet
    public void OnTrackFilterButtonClick()
    {
        trackFilterSidesheet.GetComponent<Sidesheet>().FadeIn();
    }

    private void OnTrackFilterChanged()
    {
        StartCoroutine(Refresh());
    }
    #endregion

    #region Background worker
    private BackgroundWorker trackListBuilder;
    private string builderProgress;
    private bool builderDone;

    private void TrackListBuilderDoWork(object sender,
        DoWorkEventArgs e)
    {
        RemoveCachedListsAtCurrentLocation();
        subfolderList.Clear();
        trackList.Clear();
        errorTrackList.Clear();

        BuildTrackListFor(Paths.GetTrackRootFolder());
    }

    private void BuildTrackListFor(string folder)
    {
        subfolderList.Add(folder, new List<Subfolder>());
        trackList.Add(folder, new List<TrackInFolder>());
        errorTrackList.Add(folder, new List<ErrorInTrack>());

        foreach (string file in Directory.EnumerateFiles(
            folder, "*.zip"))
        {
            // Attempt to extract this archive.
            builderProgress = Locale.GetStringAndFormat(
                "select_track_extracting_text", file);
            try
            {
                ExtractZipFile(file);
            }
            catch (Exception ex)
            {
                // Log error and move on.
                Debug.LogError(ex.ToString());
            }
        }

        foreach (string dir in Directory.EnumerateDirectories(
            folder))
        {
            builderProgress = Locale.GetStringAndFormat(
                "select_track_scanning_text", dir);

            // Is there a track?
            string possibleTrackFile = Path.Combine(
                dir, Paths.kTrackFilename);
            if (!File.Exists(possibleTrackFile))
            {
                Subfolder subfolder = new Subfolder()
                {
                    path = dir
                };

                // Look for eyecatch, if any.
                string pngEyecatch = Path.Combine(dir,
                    Paths.kSubfolderEyecatchPngFilename);
                if (File.Exists(pngEyecatch))
                {
                    subfolder.eyecatchFullPath = pngEyecatch;
                }
                string jpgEyecatch = Path.Combine(dir,
                    Paths.kSubfolderEyecatchJpgFilename);
                if (File.Exists(jpgEyecatch))
                {
                    subfolder.eyecatchFullPath = jpgEyecatch;
                }

                // Record as a subfolder.
                subfolderList[folder].Add(subfolder);

                // Build recursively.
                BuildTrackListFor(dir);

                continue;
            }

            // Attempt to load track.
            Track track = null;
            try
            {
                track = Track.LoadFromFile(possibleTrackFile) as Track;
            }
            catch (Exception ex)
            {
                errorTrackList[folder].Add(new ErrorInTrack()
                {
                    trackFile = possibleTrackFile,
                    message = ex.Message
                });
                continue;
            }

            trackList[folder].Add(new TrackInFolder()
            {
                folder = dir,
                track = track
            });
        }
    }

    private void TrackListBuilderCompleted(object sender, 
        RunWorkerCompletedEventArgs e)
    {
        builderDone = true;
        if (e.Error != null)
        {
            Debug.LogError(e.Error);
        }
    }

    private void ExtractZipFile(string zipFilename)
    {
        Debug.Log("Extracting: " + zipFilename);

        using (FileStream fileStream = File.OpenRead(zipFilename))
        using (ICSharpCode.SharpZipLib.Zip.ZipFile zipFile = new
            ICSharpCode.SharpZipLib.Zip.ZipFile(fileStream))
        {
            byte[] buffer = new byte[4096];  // Recommended length

            foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry entry in
                zipFile)
            {
                if (string.IsNullOrEmpty(
                    Path.GetDirectoryName(entry.Name)))
                {
                    Debug.Log($"Ignoring due to not being in a folder: {entry.Name} in {zipFilename}");
                    continue;
                }

                if (entry.IsDirectory)
                {
                    Debug.Log($"Ignoring empty folder: {entry.Name} in {zipFilename}");
                    continue;
                }

                string extractedFilename = Path.Combine(
                    currentLocation, entry.Name);
                Debug.Log($"Extracting {entry.Name} in {zipFilename} to: {extractedFilename}");

                Directory.CreateDirectory(Path.GetDirectoryName(
                    extractedFilename));
                using var inputStream = zipFile.GetInputStream(entry);
                using FileStream outputStream = File.Create(
                    extractedFilename);
                ICSharpCode.SharpZipLib.Core.StreamUtils.Copy(
                    inputStream, outputStream, buffer);
            }
        }

        Debug.Log($"Extract successful. Deleting: {zipFilename}");
        File.Delete(zipFilename);
    }
    #endregion

    #region Clicks on cards
    public void OnRefreshButtonClick()
    {
        RemoveCachedListsAtCurrentLocation();
        StartCoroutine(Refresh());
    }

    public void OnGoUpButtonClick()
    {
        currentLocation = new DirectoryInfo(currentLocation)
            .Parent.FullName;
        StartCoroutine(Refresh());
    }

    private void OnSubfolderCardClick(GameObject o)
    {
        currentLocation = cardToSubfolder[o];
        StartCoroutine(Refresh());
    }

    protected virtual void OnTrackCardClick(GameObject o)
    {
        GameSetup.trackPath = Path.Combine(cardToTrack[o].folder, 
            Paths.kTrackFilename);
        GameSetup.track = cardToTrack[o].track;
        GameSetup.trackOptions = Options.instance
            .GetPerTrackOptions(GameSetup.track);
        PanelTransitioner.TransitionTo(selectPatternPanel,
            TransitionToPanel.Direction.Right);
    }

    private void OnErrorCardClick(GameObject o)
    {
        string error = cardToError[o];
        messageDialog.Show(error);
    }

    protected virtual void OnNewTrackCardClick()
    {
        throw new NotImplementedException(
            "SelectTrackPanel in the game scene should not show the New Track card.");
    }
    #endregion
}
