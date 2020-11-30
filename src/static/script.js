const input = document.getElementById("url_input");
const btn = document.getElementById("download_button");
console.log(msg);

const inputHandler = function(e) {
	if (e.target.value.includes("youtube.com") || e.target.value.includes("soundcloud.com")) {
		console.log("test0");
		document.getElementsByClassName("error")[0].style.color = "green";
		msg.style.display = "none";
		msg.style.color = "green";
	}
	else {
		console.log("test1");
		document.getElementsByClassName("error")[0].style.color = "yellow";
		msg.style.display = "block";
		msg.style.color = "white";
	}	
}

btn.addEventListener("btn", inputHandler);
	