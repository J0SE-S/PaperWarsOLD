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
        if (Player != null) {
		    transform.position = new Vector3(Player.transform.position.x, Player.transform.position.y, -10);
            GetComponent<MainScript>().PlayerWaypoint.SetActive(true);
            Vector3[] v = new Vector3[4];
            GetComponent<NetworkManager>().GameCanvas.GetComponent<RectTransform>().GetWorldCorners(v);
            //GetComponent<MainScript>().PlayerWaypoint.transform.position = new Vector3(transform.position.x + v[2].x / 2 + transform.position.x / 1000, transform.position.y + v[2].y / 2 + transform.position.y / 1000, -0.2f);
            GetComponent<MainScript>().PlayerWaypoint.transform.localPosition = new Vector3(250 + transform.position.x * 150 / 50000, 95 + transform.position.y * 150 / 50000, -1.5f);
            GetComponent<NetworkManager>().Client.Send(Message.Create(MessageSendMode.Reliable, NetworkManager.MessageId.EntityMovement).AddFloat(Player.transform.position.x).AddFloat(Player.transform.position.y));
        } else {
            transform.position = new Vector3(-10000, -10000, -10);
			GetComponent<MainScript>().PlayerWaypoint.SetActive(false);
        }
     }

    void FixedUpdate() {
        if (Player != null) {
   	        Player.GetComponent<Rigidbody2D>().velocity = new Vector2(horizontal * runSpeed, vertical * runSpeed);
        }
    }
}
