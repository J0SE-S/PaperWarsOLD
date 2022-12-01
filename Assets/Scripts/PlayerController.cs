using Riptide;
using Riptide.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour {
    float horizontal;
    float vertical;
    public float moveLimiter;
    public float runSpeed;
    public GameObject Player;

    void Update() {
        horizontal = Input.GetAxisRaw("Horizontal"); 
        vertical = Input.GetAxisRaw("Vertical");
        if (horizontal != 0 && vertical != 0) {
            horizontal *= moveLimiter;
      	    vertical *= moveLimiter;
   	    }
        if (Player == gameObject) {
            Vector3 tempPosition = transform.position;
            tempPosition.z = -10;
            if (transform.position.x <= 888.725f) {
                tempPosition.x = 888.725f;
            }
            if (transform.position.y <= 499.49f) {
                tempPosition.y = 499.49f;
            }
            if (transform.position.x >= 49111.275f) {
                tempPosition.x = 49111.275f;
            }
            if (transform.position.y >= 49500.51f) {
                tempPosition.y = 49500.51f;
            }
            transform.position = tempPosition;
            GetComponent<MainScript>().PlayerWaypoint.SetActive(false);
            if (GetComponent<MainScript>().buildingPlacementMode == false) {
                GetComponent<NetworkManager>().Client.Send(Message.Create(MessageSendMode.Reliable, NetworkManager.MessageId.Join).AddString(Camera.main.GetComponent<MainScript>().saveFile.uuid).AddString(Camera.main.GetComponent<MainScript>().saveFile.username));
                Player = null;
            }
        } else if (Player != null) {
		    transform.position = new Vector3(Player.transform.position.x, Player.transform.position.y, -10);
            GetComponent<MainScript>().PlayerWaypoint.SetActive(true);
            Vector3[] v = new Vector3[4];
            GetComponent<NetworkManager>().GameCanvas.GetComponent<RectTransform>().GetWorldCorners(v);
            GetComponent<MainScript>().PlayerWaypoint.transform.localPosition = new Vector3(250 + transform.position.x * 150 / 50000, 95 + transform.position.y * 150 / 50000, -1.5f);
            GetComponent<NetworkManager>().Client.Send(Message.Create(MessageSendMode.Reliable, NetworkManager.MessageId.EntityMovement).AddFloat(Player.transform.position.x).AddFloat(Player.transform.position.y));
        } else {
            transform.position = new Vector3(-10000, -10000, -10);
			GetComponent<MainScript>().PlayerWaypoint.SetActive(false);
        }
    }

    void FixedUpdate() {
        if (Player == gameObject) {
            Player.GetComponent<Rigidbody2D>().velocity = new Vector2(horizontal * runSpeed * 5, vertical * runSpeed * 5);
        } else if (Player != null) {
   	        Player.GetComponent<Rigidbody2D>().velocity = new Vector2(horizontal * runSpeed, vertical * runSpeed);
        }
    }
}
