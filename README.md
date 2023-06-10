# Oasis

Unity version for the moment will be 2021.3 LTS (2021.3.15f1 LTS at the time of writing), as the Arcade Simulator project is built on 2021.2.7f1.  

We are using Built-in Render Pipeline for the moment, this is due to lightmap baking being impractical for user generated environments (Arcades, Crazy Golf courses, race tracks etc)... all Unity untime-lightmap baking solutions seemed lacking (on device, remote lightmap baking server etc), and by the time this is a mature product, RTX cards may be relatively standard and affordable, meaning dynamic light/shadows with no baked maps required.
