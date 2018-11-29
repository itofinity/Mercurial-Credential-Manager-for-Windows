# -*- coding: utf-8 -*-
#
# mercurial-credential-manager
# 
# Based extensively on the Mercurial_Keyring
# Copyright (c) 2009 Marcin Kasperski <Marcin.Kasperski@mekk.waw.pl>

import urllib2
import smtplib
import socket
import os
import sys
import re

from mercurial import util, sslutil
from mercurial.i18n import _
from mercurial.url import passwordmgr
from mercurial import mail
from mercurial.mail import SMTPS, STARTTLS
from mercurial import encoding

# pylint: disable=invalid-name, line-too-long, protected-access, too-many-arguments

###########################################################################
# Specific import trickery
###########################################################################


def import_meu():
    """
    Convoluted import of mercurial_extension_utils, which helps
    TortoiseHg/Win setups. This routine and it's use below
    performs equivalent of
        from mercurial_extension_utils import monkeypatch_method
    but looks for some non-path directories.
    """
    try:
        import mercurial_extension_utils
    except ImportError:
        my_dir = os.path.dirname(__file__)
        sys.path.extend([
            # In the same dir (manual or site-packages after pip)
            my_dir,
            # Developer clone
            os.path.join(os.path.dirname(my_dir), "extension_utils"),
            # Side clone
            os.path.join(os.path.dirname(my_dir), "mercurial-extension_utils"),
        ])
        try:
            import mercurial_extension_utils
        except ImportError:
            raise util.Abort(_("""Can not import mercurial_extension_utils.
Please install this module in Python path.
See Installation chapter in https://bitbucket.org/Mekk/mercurial_keyring/ for details
(and for info about TortoiseHG on Windows, or other bundled Python)."""))
    return mercurial_extension_utils

meu = import_meu()
monkeypatch_method = meu.monkeypatch_method

#################################################################
# Actual implementation
#################################################################

############################################################
# Various utils
############################################################

def _debug(ui, msg):
    """Generic debug message"""
    ui.debug("mercurial-credential-manager: " + msg + "\n")


class PwdCache(object):
    """Short term cache, used to preserve passwords
    if they are used twice during a command"""
    def __init__(self):
        self._cache = {}

    def store(self, realm, url, user, pwd):
        """Saves password"""
        cache_key = (realm, url, user)
        self._cache[cache_key] = pwd

    def check(self, realm, url, user):
        """Checks for cached password"""
        cache_key = (realm, url, user)
        return self._cache.get(cache_key)


_re_http_url = re.compile(r'^https?://')


############################################################
# HTTP password management
############################################################


class HTTPPasswordHandler(object):
    """
    Actual implementation of password handling (user prompting,
    configuration file searching, keyring save&restore).

    Object of this class is bound as passwordmgr attribute.
    """
    def __init__(self):
        self.pwd_cache = PwdCache()
        self.last_reply = None

    # Markers and also names used in debug notes. Password source
    SRC_URL = "repository URL"
    SRC_CFGAUTH = "hgrc"
    SRC_MEMCACHE = "temporary cache"
    SRC_URLCACHE = "urllib temporary cache"
    SRC_KEYRING = "keyring"

    @staticmethod
    def get_guiprompt(ui):
        # check for an override GUI
        gui_prompt = os.environ.get('MCM_GUI')
        if gui_prompt and os.path.isfile(gui_prompt):
            return gui_prompt

        # check for the default GUI
        if os.name == "nt":
            gui_prompt = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mercurial-credential-manager.exe")
            if os.path.isfile(gui_prompt):
                return gui_prompt
        else:
            gui_prompt = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mercurial-credential-manager")
            if os.path.isfile(gui_prompt):
                return gui_prompt

        _debug(ui,_("mercurial-credential-manager: GUI not found as [mercurial-credential-manager(.exe)] or [%s].\n") % (os.environ.get('MCM_GUI')))
        return None

    def get_credentials(self, pwmgr, realm, authuri, skip_caches=False):
        """
        Looks up for user credentials in various places, returns them
        and information about their source.

        Used internally inside find_auth and inside informative
        commands (thiis method doesn't cache, doesn't detect bad
        passwords etc, doesn't prompt interactively, doesn't store
        password in keyring).

        Returns: user, password, SRC_*, actual_url

        If not found, password and SRC is None, user can be given or
        not, url is always set
        """
        ui = pwmgr.ui

        parsed_url, url_user, url_passwd = self.unpack_url(authuri)
        base_url = str(parsed_url)
        ui.debug(_('mercurial-credential-manager: base url: %s, url user: %s, url pwd: %s\n') %
                 (base_url, url_user or '', url_passwd and '******' or ''))

        # Extract username (or password) stored directly in url
        if url_user and url_passwd:
            return url_user, url_passwd, self.SRC_URL, base_url

        # Extract data from urllib (in case it was already stored)
        if isinstance(pwmgr, urllib2.HTTPPasswordMgrWithDefaultRealm):
            urllib_user, urllib_pwd = \
                urllib2.HTTPPasswordMgrWithDefaultRealm.find_user_password(
                    pwmgr, realm, authuri)
        else:
            urllib_user, urllib_pwd = pwmgr.passwddb.find_user_password(
                realm, authuri)
        if urllib_user and urllib_pwd:
            return urllib_user, urllib_pwd, self.SRC_URLCACHE, base_url

        actual_user = url_user or urllib_user

        # Consult configuration to normalize url to prefix, and find username
        # (and maybe password)
        auth_user, auth_pwd, keyring_url = self.get_url_config(
            ui, parsed_url, actual_user)
        if auth_user and actual_user and (actual_user != auth_user):
            raise util.Abort(_('mercurial-credential-manager: username for %s specified both in repository path (%s) and in .hg/hgrc/[auth] (%s). Please, leave only one of those' % (base_url, actual_user, auth_user)))
        if auth_user and auth_pwd:
            return auth_user, auth_pwd, self.SRC_CFGAUTH, keyring_url

        actual_user = actual_user or auth_user

        if skip_caches:
            return actual_user, None, None, keyring_url

        # Check memory cache (reuse )
        # Checking the memory cache (there may be many http calls per command)
        cached_pwd = self.pwd_cache.check(realm, keyring_url, actual_user)
        if cached_pwd:
            return actual_user, cached_pwd, self.SRC_MEMCACHE, keyring_url

        return actual_user, None, None, keyring_url

    @staticmethod
    def prompt_interactively(ui, user, realm, url):
        """Actual interactive prompt"""
        if not ui.interactive():
			raise util.Abort(_('keyring: http authorization required but program used in non-interactive mode'))

        if not user:
            ui.status(_("keyring: username not specified in hgrc (or in url). Password will not be saved.\n"))

        ui.write(_("http authorization required\n"))
        ui.status(_("realm: %s\n") % realm)
        ui.status(_("url: %s\n") % url)
        if user:
            ui.write(_("user: %s (fixed in hgrc or url)\n" % user))
        else:
            user = ui.prompt(_("user:"), default=None)
        pwd = ui.getpass(_("password: "))
        return user, pwd
    
    def find_auth(self, pwmgr, realm, authuri, req):
        """
        Actual implementation of find_user_password - different
        ways of obtaining the username and password.

        Returns pair username, password
        """

        ui = pwmgr.ui

        after_bad_auth = self._after_bad_auth(ui, realm, authuri, req)

        # Look in url, cache, etc
        user, pwd, src, final_url = self.get_credentials(
            pwmgr, realm, authuri, skip_caches=after_bad_auth)
        if pwd:
            if src != self.SRC_MEMCACHE:
                self.pwd_cache.store(realm, final_url, user, pwd)
            self._note_last_reply(realm, authuri, user, req)
            _debug(ui, _("Password found in " + src))
            return user, pwd

        parsed_url, url_user_unused, url_passwd_unused = self.unpack_url(authuri)
        
        # check for an override GUI
        gui_prompt = self.get_guiprompt(ui)

        if gui_prompt and os.path.isfile(gui_prompt):
            _debug(ui,_("mercurial-credential-manager: GUI GET: %s.\n") % (gui_prompt))
            from subprocess import Popen, PIPE
            proc = Popen([gui_prompt, "GET"], stdin=PIPE, stdout=PIPE, stderr=PIPE)
            if user is None:
                userprompt = ""
            else:
                userprompt = "username=%s\n" % (user)
            command = "%shost=%s\nprotocol=https\npath=%s\n\n" % (userprompt, parsed_url.host, parsed_url.path)
            _debug(ui,_("mercurial-credential-manager: GUI GET:command= %s.\n") % (command))
            output,erroutput = proc.communicate(input=command)
            # Uncomment to see details, but this includes the password in plan text _debug(ui,_("mercurial-credential-manager: GUI GET:output=[%s]\n") % (output))
            import re
            username = re.findall(r'username=(.+)', output)
            password = re.findall(r'password=(.+)', output)
            if username[0]:
                user = username[0]
            if password[0]:
                pwd = password[0]
            if pwd:
                self._note_last_reply(realm, authuri, user, req)
                return user, pwd
        else:
            # Last resort: interactive prompt
            user, pwd = self.prompt_interactively(ui, user, realm, final_url)
            if pwd:
                self._note_last_reply(realm, authuri, user, req)
                return user, pwd

        self._note_last_reply(realm, authuri, user, req)
        return user, pwd

    def get_url_config(self, ui, parsed_url, user):
        """
        Checks configuration to decide whether/which username, prefix,
        and password are configured for given url. Consults [auth] section.

        Returns tuple (username, password, prefix) containing elements
        found. username and password can be None (if unset), if prefix
        is not found, url itself is returned.
        """
        base_url = str(parsed_url)

        from mercurial.httpconnection import readauthforuri
        _debug(ui, _("Checking for hgrc info about url %s, user %s") % (base_url, user))
        res = readauthforuri(ui, base_url, user)
        # If it user-less version not work, let's try with added username to handle
        # both config conventions
        if (not res) and user:
            parsed_url.user = user
            res = readauthforuri(ui, str(parsed_url), user)
            parsed_url.user = None
        if res:
            group, auth_token = res
        else:
            auth_token = None

        if auth_token:
            username = auth_token.get('username')
            password = auth_token.get('password')
            prefix = auth_token.get('prefix')
        else:
            username = user
            password = None
            prefix = None

        password_url = self.password_url(base_url, prefix)

        _debug(ui, _("Password url: %s, user: %s, password: %s (prefix: %s)") % (
            password_url, username, '********' if password else '', prefix))

        return username, password, password_url

    def _note_last_reply(self, realm, authuri, user, req):
        """
        Internal helper. Saves info about auth-data obtained,
        preserves them in last_reply, and returns pair user, pwd
        """
        self.last_reply = dict(realm=realm, authuri=authuri,
                               user=user, req=req)

    def _after_bad_auth(self, ui, realm, authuri, req):
        """
        If we are called again just after identical previous
        request, then the previously returned auth must have been
        wrong. So we note this to force password prompt (and avoid
        reusing bad password indefinitely).

        This routine checks for this condition.
        """
        if self.last_reply:
            if (self.last_reply['realm'] == realm) \
               and (self.last_reply['authuri'] == authuri) \
               and (self.last_reply['req'] == req):
                _debug(ui, _("Working after bad authentication, cached passwords not used %s") % str(self.last_reply))

                parsed_url, url_user, url_passwd = self.unpack_url(authuri)
                gui_prompt = self.get_guiprompt(ui)
                if gui_prompt and os.path.isfile(gui_prompt):
                    _debug(ui,_("mercurial-credential-manager: GUI ERASE: %s.\n") % (gui_prompt))
                    from subprocess import Popen, PIPE
                    proc = Popen([gui_prompt, "ERASE"], stdin=PIPE, stdout=PIPE, stderr=PIPE)
                    if url_user is None:
                        userprompt = ""
                    else:
                        userprompt = "username=%s\n" % (user)
                    command = "%shost=%s\nprotocol=https\npath=%s\n\n" % (userprompt, parsed_url.host, parsed_url.path)
                    _debug(ui,_("mercurial-credential-manager: GUI ERASE:command= %s.\n") % (command))
                    output,erroutput = proc.communicate(input=command)
                    _debug(ui,_("mercurial-credential-manager: GUI ERASE:output=[%s]\n") % (output))
                return True
        return False

    @staticmethod
    def password_url(base_url, prefix):
        """Calculates actual url identifying the password. Takes
        configured prefix under consideration (so can be shorter
        than repo url)"""
        if not prefix or prefix == '*':
            return base_url
        scheme, hostpath = base_url.split('://', 1)
        p = prefix.split('://', 1)
        if len(p) > 1:
            prefix_host_path = p[1]
        else:
            prefix_host_path = prefix
        password_url = scheme + '://' + prefix_host_path
        return password_url

    @staticmethod
    def unpack_url(authuri):
        """
        Takes original url for which authentication is attempted and:

        - Strips query params from url. Used to convert urls like
          https://repo.machine.com/repos/apps/module?pairs=0000000000000000000000000000000000000000-0000000000000000000000000000000000000000&cmd=between
          to
          https://repo.machine.com/repos/apps/module

        - Extracts username and password, if present, and removes them from url
          (so prefix matching works properly)

        Returns url, user, password
        where url is mercurial.util.url object already stripped of all those
        params.
        """
        # mercurial.util.url, rather handy url parser
        parsed_url = util.url(authuri)
        parsed_url.query = ''
        parsed_url.fragment = None
        # Strip arguments to get actual remote repository url.
        # base_url = "%s://%s%s" % (parsed_url.scheme, parsed_url.netloc,
        #                       parsed_url.path)
        user = parsed_url.user
        passwd = parsed_url.passwd
        parsed_url.user = None
        parsed_url.passwd = None

        return parsed_url, user, passwd


############################################################
# Mercurial monkey-patching
############################################################


@monkeypatch_method(passwordmgr)
def find_user_password(self, realm, authuri):
    """
    keyring-based implementation of username/password query
    for HTTP(S) connections

    Passwords are saved in gnome keyring, OSX/Chain or other platform
    specific storage and keyed by the repository url
    """
    # Extend object attributes
    if not hasattr(self, '_pwd_handler'):
        self._pwd_handler = HTTPPasswordHandler()

    if hasattr(self, '_http_req'):
        req = self._http_req
    else:
        req = None

    return self._pwd_handler.find_auth(self, realm, authuri, req)


@monkeypatch_method(urllib2.AbstractBasicAuthHandler, "http_error_auth_reqed")
def basic_http_error_auth_reqed(self, authreq, host, req, headers):
    """Preserves current HTTP request so it can be consulted
    in find_user_password above"""
    self.passwd._http_req = req
    try:
        return basic_http_error_auth_reqed.orig(self, authreq, host, req, headers)
    finally:
        self.passwd._http_req = None


@monkeypatch_method(urllib2.AbstractDigestAuthHandler, "http_error_auth_reqed")
def digest_http_error_auth_reqed(self, authreq, host, req, headers):
    """Preserves current HTTP request so it can be consulted
    in find_user_password above"""
    self.passwd._http_req = req
    try:
        return digest_http_error_auth_reqed.orig(self, authreq, host, req, headers)
    finally:
        self.passwd._http_req = None

buglink = 'https://github.com/mminns/mercurial_credential_manager_for_windows'