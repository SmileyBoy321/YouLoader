const input = document.getElementById("url_input");
const btn = document.getElementById("download_button");
const err = document.getElementById("error");

const inputHandler = function(e) {
	if (e.target.value.includes("youtube.com") || e.target.value.includes("soundcloud.com") || e.target.value == "") {
		err.style.display = "none";
        btn.disabled = false;
	}
	else {
		err.style.display = "block";
        btn.disabled = true;
	}	
}

input.addEventListener("input", inputHandler);
	
