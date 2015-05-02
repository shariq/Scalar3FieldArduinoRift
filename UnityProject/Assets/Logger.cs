using UnityEngine;
using System.Collections;
using System.IO.Ports;

public class Logger : MonoBehaviour {
	public GameObject TrackedObject;
	int[, ,] sizes;
	int[, ,] pressures;
	GameObject[, ,] spheres;

	float percentCutoff;

	SerialPort stream;
	Vector3 initialPosition;
	
	static float resolution = 0.001f;
	// 1mm

	static int voxel_width = 100;
	static int voxel_height = 200;
	static int voxel_depth = 100;

	static int minPressure = 0;
	static int maxPressure = 20 * 1024;

	static string COM_PORT = "COM3";

	// Use this for initialization
	void Start () {
		percentCutoff = 0f;
		stream = new SerialPort(COM_PORT, 115200);
		stream.Open ();
		sizes = new int[voxel_width,voxel_height,voxel_depth];
		pressures = new int[voxel_width,voxel_height,voxel_depth];
		spheres = new GameObject[voxel_width, voxel_height, voxel_depth];
		for (int i = 0; i < voxel_width; i++) {
			for (int j = 0; j < voxel_height; j++) {
				for (int k = 0; k < voxel_depth; k++) {
					sizes[i,j,k] = 0;
					pressures[i,j,k] = 0;
					//spheres[i,j,k] = null;
				}
			}
		}
		initialPosition = Vector3.one*1000;
	}
	 
	// Update is called once per frame
	void Update () {
		ProcessCutoff ();

		if (SmallEnough(5)) {
			if (initialPosition ==  Vector3.one*1000) {
				initialPosition = TrackedObject.transform.position;
			}
			int pressure = GrabPressure();
			Vector3 offset = (TrackedObject.transform.position - initialPosition) / resolution;
			offset += new Vector3(voxel_width/2, voxel_height/2, voxel_depth/2);
			Debug.Log("Offset: " + offset.ToString());
			int i = (int)Mathf.Round(offset.x);
			int j = (int)Mathf.Round(offset.y);
			int k = (int)Mathf.Round(offset.z);
			if (i >=0 && i < voxel_width && j >= 0 && j < voxel_height && k>=0 &&k<voxel_depth) {
				ProcessFrame(pressure, i, j, k);
			}
		}
	}

	void OnDestroy() {
		stream.Close ();
	}

	int GrabPressure() {
		stream.DiscardInBuffer();
		stream.Write(" ");
		return int.Parse(stream.ReadLine());
	}

	void ProcessFrame(int pressure, int i, int j, int k) {
		Debug.Log(pressure + " sampled from " + pressures[i,j,k] + " with size " + sizes[i,j,k]);
		pressures[i,j,k] = (pressures[i,j,k] * sizes[i,j,k] + pressure)/(sizes[i,j,k]+1);
		sizes[i,j,k] += 1;
		
		if (sizes[i,j,k] == 1) {
			spheres[i,j,k] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			spheres[i,j,k].transform.position = ijkToPosition(i, j, k);
		}
		
		// adjust size and adjust color
		spheres[i,j,k].transform.localScale = Vector3.one * SizeToScale(sizes[i,j,k]);
		spheres[i,j,k].GetComponent<Renderer>().material.color = PressureToColor(pressures[i,j,k]);

		if (pressures [i, j, k] < (int)(percentCutoff * (maxPressure - minPressure)) + minPressure) {
			spheres[i,j,k].GetComponent<Renderer>().enabled = false;
		}
	}

	void ProcessCutoff() {
		if (Input.GetAxis ("Horizontal") == 0f) {
			return;
		}
		float oldPercentCutoff = percentCutoff;
		percentCutoff += Input.GetAxis("Horizontal") * 0.05f;
		if (percentCutoff < 0f) {
			percentCutoff = 0f;
		}
		if (percentCutoff > 1f) {
			percentCutoff = 1f;
		}
		Debug.Log("Percent cutoff: " + percentCutoff);
		int oldPressureThreshold = (int)(oldPercentCutoff * (maxPressure - minPressure)) + minPressure;
		int newPressureThreshold = (int)(percentCutoff * (maxPressure - minPressure)) + minPressure;
		// check all spheres, disable and enable
		if (newPressureThreshold < oldPressureThreshold) {
			// reenable spheres between old and new
			for (int i = 0; i < voxel_width; i++) {
				for (int j = 0; j < voxel_height; j++) {
					for (int k = 0; k < voxel_depth; k++) {
						if (sizes[i,j,k] > 0 && pressures[i,j,k] >= newPressureThreshold) {
							spheres[i,j,k].GetComponent<Renderer>().enabled = true;
						}
					}
				}
			}
		} else {
			// disable spheres between old and new
			for (int i = 0; i < voxel_width; i++) {
				for (int j = 0; j < voxel_height; j++) {
					for (int k = 0; k < voxel_depth; k++) {
						if (sizes[i,j,k] > 0 && pressures[i,j,k] <= newPressureThreshold) {
							spheres[i,j,k].GetComponent<Renderer>().enabled = false;
						}
					}
				}
			}
		}
		// maxPressure is always shown; threshold only hides whatever is below it

	}

	bool SmallEnough(double threshold) {
		float x_from_0 = TrackedObject.transform.eulerAngles.x;
		float y_from_0 = TrackedObject.transform.eulerAngles.y;
		float z_from_0 = TrackedObject.transform.eulerAngles.z;
		if (x_from_0 > 180.0) {
			x_from_0 -= 360f;
		}
		if (y_from_0 > 180.0) {
			y_from_0 -= 360f;
		}
		if (z_from_0 > 180.0) {
			z_from_0 -= 360f;
		}
		return (new Vector3(x_from_0, y_from_0, z_from_0)).magnitude < threshold;
	}
	
	Color PressureToColor(int pressure) {
		float percentPressure = (float)pressure/((float)maxPressure);
		return new Color(percentPressure, 0f, 1f-percentPressure, 1f);
	}

	float SizeToScale(int size) {
		if (size < 20) {
			return 0.5f * Mathf.Pow(size/20f, 0.25f); 
		} else {
			return 0.5f;
		}
	}

	Vector3 ijkToPosition(int i, int j, int k) {
		return new Vector3(i,j,k) * 0.5f;
	}
}